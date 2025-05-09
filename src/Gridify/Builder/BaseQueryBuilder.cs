using System;
using System.ComponentModel;
using System.Linq.Expressions;
using Gridify.Reflection;
using Gridify.Syntax;

namespace Gridify.Builder;

internal abstract class BaseQueryBuilder
{
   internal static MemberNullPropagationVisitor MemberNullPropagatorVisitor { get; } = new();
}

public abstract class BaseQueryBuilder<TQuery, T>(IGridifyMapper<T> mapper)
   where TQuery : class
{
   public TQuery Build(ExpressionSyntax expression)
   {
      var (query, _) = BuildQuery(expression);
      return query;
   }

   protected abstract TQuery? BuildNestedQuery(
      Expression body, IGMap<T> gMap, ValueExpressionSyntax value, ISyntaxNode op);

   protected abstract TQuery BuildAlwaysTrueQuery();

   protected abstract TQuery BuildAlwaysFalseQuery(ParameterExpression parameter);

   protected abstract TQuery? CheckIfCanMergeQueries(
      (TQuery query, bool isNested) leftQuery,
      (TQuery query, bool isNested) rightQuery,
      SyntaxKind op);

   protected abstract object? BuildQueryAccordingToValueType(
      Expression body,
      ParameterExpression parameter,
      object? value,
      ISyntaxNode op,
      ValueExpressionSyntax valueExpression);

   protected abstract TQuery CombineWithAndOperator(TQuery left, TQuery right);

   protected abstract TQuery CombineWithOrOperator(TQuery left, TQuery right);

   private (TQuery Query, bool IsNested) BuildQueryFromBinaryExpression(BinaryExpressionSyntax? bExp, bool isParenthesisOpen = false)
   {
      if (bExp!.Left is FieldExpressionSyntax && bExp.Right is ValueExpressionSyntax)
         try
         {
            return ConvertBinaryExpressionSyntaxToQuery(bExp)
                   ?? throw new GridifyFilteringException("Invalid expression");
         }
         catch (GridifyMapperException)
         {
            if (mapper.Configuration.IgnoreNotMappedFields)
               return (BuildAlwaysTrueQuery(), false);

            throw;
         }

      (TQuery query, bool isNested) leftQuery;
      (TQuery query, bool isNested) rightQuery;

      if (bExp.Left is ParenthesizedExpressionSyntax lpExp)
         leftQuery = BuildQuery(lpExp.Expression, true);
      else
         leftQuery = BuildQuery(bExp.Left);


      if (bExp.Right is ParenthesizedExpressionSyntax rpExp)
         rightQuery = BuildQuery(rpExp.Expression, true);
      else
         rightQuery = BuildQuery(bExp.Right);

      // check for nested collections
      if (isParenthesisOpen &&
          CheckIfCanMergeQueries(leftQuery, rightQuery, bExp.OperatorToken.Kind) is { } mergedResult)
         return (mergedResult, true);

      var result = bExp.OperatorToken.Kind switch
      {
         SyntaxKind.And => CombineWithAndOperator(leftQuery.query, rightQuery.query),
         SyntaxKind.Or => CombineWithOrOperator(leftQuery.query, rightQuery.query),
         _ => throw new GridifyFilteringException($"Invalid expression Operator '{bExp.OperatorToken.Kind}'")
      };
      return (result, false);
   }

   private (TQuery Query, bool IsNested) BuildQuery(ExpressionSyntax expression, bool isParenthesisOpen = false)
   {
      while (true)
         switch (expression.Kind)
         {
            case SyntaxKind.BinaryExpression:
            {
               var bExp = expression as BinaryExpressionSyntax;
               return BuildQueryFromBinaryExpression(bExp, isParenthesisOpen);
            }
            case SyntaxKind.ParenthesizedExpression: // first entrypoint only
            {
               var pExp = expression as ParenthesizedExpressionSyntax;
               return BuildQuery(pExp!.Expression, true);
            }
            default:
               throw new GridifyFilteringException($"Invalid expression format '{expression.Kind}'.");
         }
   }

   private (TQuery, bool IsNested)? ConvertBinaryExpressionSyntaxToQuery(BinaryExpressionSyntax binarySyntax)
   {
      var fieldExpression = binarySyntax.Left as FieldExpressionSyntax;

      var left = fieldExpression?.FieldToken.Text.Trim();
      var right = binarySyntax.Right as ValueExpressionSyntax;
      var op = binarySyntax.OperatorToken;

      if (left == null || right == null) return null;

      var gMap = mapper.GetGMap(left);
      if (gMap == null) throw new GridifyMapperException($"Mapping '{left}' not found");
      var mapTarget = gMap.To;

      var hasIndexer = fieldExpression?.Indexer != null;
      if (hasIndexer)
         mapTarget = UpdateIndexerKey(mapTarget, fieldExpression!.Indexer!);

      var isNested = ((GMap<T>)gMap).IsNestedCollection();
      if (isNested)
      {
         var result = BuildNestedQuery(mapTarget.Body, gMap, right, op);
         if (result == null) return null;
         return (result, isNested);
      }

      var query = BuildQuery(mapTarget.Body, mapTarget.Parameters[0], right, op, gMap.Convertor, false);
      if (query == null) return null;

      if (hasIndexer)
         query = AddIndexerNullCheck(mapTarget, query);
      else
         query = AddNullPropagator(mapTarget, query);

      return ((TQuery)query, false);
   }


   private object AddNullPropagator(LambdaExpression mapTarget, object query)
   {
      if (!mapper.Configuration.AvoidNullReference)
         return query;

      var mainQuery = query as LambdaExpression;
      if (mainQuery == null)
         return query;

      var nullPropagatedExpression = BaseQueryBuilder.MemberNullPropagatorVisitor.Visit(mapTarget) as LambdaExpression;
      if (mainQuery.Body is MethodCallExpression methodExp
         && nullPropagatedExpression!.Body.Type != typeof(object)
         && methodExp.Arguments.Count == 1)
      {
         var call = Expression.Call(methodExp.Method!, nullPropagatedExpression!.Body);
         return Expression.Lambda(call, mainQuery.Parameters);
      }

      var body = mainQuery.Body;
      if (body is not BinaryExpression bExp)
         return query;

      if (bExp.Method == null
         || nullPropagatedExpression!.Body.Type == typeof(object)
         || nullPropagatedExpression.Body is MethodCallExpression)
      {
         return query;
      }
      var newExp = Expression.Call(bExp.Method!, nullPropagatedExpression.Body, bExp.Right);
      return Expression.Lambda(newExp, mainQuery.Parameters);
   }

   private object AddIndexerNullCheck(LambdaExpression mapTarget, object query)
   {
      if (mapper.Configuration.DisableCollectionNullChecks || mapper.Configuration.EntityFrameworkCompatibilityLayer)
         return query;

      var body = mapTarget.Body;
      if (body is UnaryExpression unaryExpression)
         body = unaryExpression.Operand;

      if (body is not MethodCallExpression methodCallExpression) return query;

      var containsKeyMethod = methodCallExpression.Object!.Type.GetMethod("ContainsKey", [mapTarget.Parameters[1].Type]);
      if (containsKeyMethod == null) return query;
      var mainQuery = (LambdaExpression)query;
      var keyNullCheck = Expression.Call(methodCallExpression.Object!, containsKeyMethod, methodCallExpression.Arguments);
      var newExp = Expression.AndAlso(keyNullCheck, mainQuery.Body);
      return Expression.Lambda(newExp, mainQuery.Parameters);
   }


   protected object? BuildQuery(
      Expression body,
      ParameterExpression parameter,
      ValueExpressionSyntax valueExpression,
      ISyntaxNode op,
      Func<string, object>? convertor,
      bool isNested)
   {
      // Remove the boxing for value types
      if (body.NodeType == ExpressionType.Convert) body = ((UnaryExpression)body).Operand;

      object? value = valueExpression.ValueToken.Text;

      // execute user custom Convertor
      if (convertor != null)
         value = convertor.Invoke(valueExpression.ValueToken.Text);

      // handle the `null` keyword in value
      if (mapper.Configuration.AllowNullSearch && op.Kind is SyntaxKind.Equal or SyntaxKind.NotEqual && value?.ToString() == "null")
         value = null;

      // type fixer
      // Check if body.Type is a nullable type and get its underlying type issue #134
      var underlyingBodyType = Nullable.GetUnderlyingType(body.Type);
      if (value is string strValue && (underlyingBodyType ?? body.Type) != strValue.GetType())
      {
         // handle bool, github issue #71
         if (body.Type == typeof(bool) && strValue is "true" or "false" or "1" or "0")
            value = strValue.ToLower() is "1" or "true";
         // handle broken guids,  github issue #2
         else if (body.Type == typeof(Guid) && !Guid.TryParse(strValue, out _)) value = Guid.NewGuid().ToString();

         var converter = TypeDescriptor.GetConverter(body.Type);
         if (converter.CanConvertFrom(typeof(string)))
            try
            {
               value = converter.ConvertFromString(value.ToString()!);
            }
            catch (ArgumentException)
            {
               // we can ignore and continue
            }
            catch (FormatException)
            {
               return BuildAlwaysFalseQuery(parameter);
            }

         if (value is DateTime dateTime)
         {
            if (mapper.Configuration.DefaultDateTimeKind.HasValue)
            {
               value = mapper.Configuration.DefaultDateTimeKind.Value switch
               {
                  DateTimeKind.Local => dateTime.ToLocalTime(),
                  DateTimeKind.Utc => dateTime.ToUniversalTime(),
                  _ => dateTime
               };
            }
         }
      }

      // handle case-Insensitive search
      if (value is not null && (valueExpression.IsCaseInsensitive
                            || (mapper.Configuration.CaseInsensitiveFiltering && !isNested && body.Type == typeof(string)))
                            && op.Kind is not SyntaxKind.GreaterThan
                            && op.Kind is not SyntaxKind.LessThan
                            && op.Kind is not SyntaxKind.GreaterOrEqualThan
                            && op.Kind is not SyntaxKind.LessOrEqualThan)
      {
         var strLowerValue = value.ToString()?.ToLower();
         value = strLowerValue;

         if (!string.IsNullOrEmpty(strLowerValue))
         {
            body = Expression.Call(body, MethodInfoHelper.GetToLowerMethod());
         }
      }

      var query = BuildQueryAccordingToValueType(body, parameter, value, op, valueExpression);
      return query;
   }

   private static LambdaExpression UpdateIndexerKey(LambdaExpression exp, string key)
   {
      var type = exp.Parameters[1].Type;
      var newValue = type == typeof(string)
         ? Expression.Constant(key, typeof(string))
         : Expression.Constant(TypeDescriptor.GetConverter(type).ConvertFromString(key), type);

      var body = new ReplaceExpressionVisitor(exp.Parameters[1], newValue).Visit(exp.Body);
      return Expression.Lambda(body, exp.Parameters);
   }
}
