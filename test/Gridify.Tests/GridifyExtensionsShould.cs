#nullable enable
using Gridify.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Gridify.Tests;

public class GridifyExtensionsShould
{
   private readonly List<TestClass> _fakeRepository;

   public GridifyExtensionsShould()
   {
      _fakeRepository = new List<TestClass>(GetSampleData());
   }

   #region "Data"

   public static IEnumerable<TestClass> GetSampleData()
   {
      var lst = new List<TestClass>();
      lst.Add(new TestClass(1, "John", null, Guid.NewGuid(), DateTime.Now, isActive: true));
      lst.Add(new TestClass(2, "Bob", null, Guid.NewGuid(), DateTime.UtcNow, isActive: true));
      lst.Add(new TestClass(3, "Jack", (TestClass)lst[0].Clone(), Guid.Empty, DateTime.Now.AddDays(2), isActive: true));
      lst.Add(new TestClass(4, "Rose", null, Guid.Parse("e2cec5dd-208d-4bb5-a852-50008f8ba366")));
      lst.Add(new TestClass(5, "Ali", null, tag: "123"));
      lst.Add(new TestClass(6, "Hamid", (TestClass)lst[0].Clone(), Guid.Parse("de12bae1-93fa-40e4-92d1-2e60f95b468c")));
      lst.Add(new TestClass(7, "Hasan", (TestClass)lst[1].Clone()));
      lst.Add(new TestClass(8, "Farhad", (TestClass)lst[2].Clone(), Guid.Empty));
      lst.Add(new TestClass(9, "Sara", null));
      lst.Add(new TestClass(10, "Jorge", null));
      lst.Add(new TestClass(11, "joe", null));
      lst.Add(new TestClass(12, "jimmy", (TestClass)lst[0].Clone()));
      lst.Add(new TestClass(13, "Nazanin", null, tag: "test0"));
      lst.Add(new TestClass(14, "Reza", null, tag: "test1"));
      lst.Add(new TestClass(15, "Korosh", (TestClass)lst[0].Clone()));
      lst.Add(new TestClass(16, "Kamran", (TestClass)lst[1].Clone()));
      lst.Add(new TestClass(17, "Saeid", (TestClass)lst[2].Clone()));
      lst.Add(new TestClass(18, "jessi=ca", null));
      lst.Add(new TestClass(19, "Ped=ram", null, tag: "test3"));
      lst.Add(new TestClass(20, "Peyman!", null));
      lst.Add(new TestClass(21, "Fereshte", null, tag: null));
      lst.Add(new TestClass(22, "LIAM", null));
      lst.Add(new TestClass(22, @"\Liam", null, tag: null, isActive: true));
      lst.Add(new TestClass(23, "LI | AM", null));
      lst.Add(new TestClass(24, "(LI,AM)", null, tag: string.Empty));
      lst.Add(new TestClass(25, "Case/i", null, tag: string.Empty));
      lst.Add(new TestClass(26, "/iCase", null));
      lst.Add(new TestClass(27, "ali reza", null));
      lst.Add(new TestClass(27, "[ali]", null));
      lst.Add(new TestClass(28, @"Esc/\pe", null));
      lst.Add(new TestClass(29, @"NullTag", null, tag: null));

      return lst;
   }

   #endregion


   #region "ApplyFiltering"

   [Fact]
   public void ApplyFiltering_ChildField()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("ChildClass!=null,ChildClass.Name=John")
         .ToList();

      var expected = _fakeRepository
         .Where(q => q.ChildClass is { Name: "John" })
         .ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_WithCustomMapper_ChildField_ShowThrowException()
   {
      var gm = new GridifyMapper<TestClass>()
         .AddMap("subId", q => q.ChildClass!.Id);

      Assert.Throws<GridifyMapperException>(() =>
      {
         var _ = _fakeRepository.AsQueryable()
            .ApplyFiltering("ChildClass.Name=John", gm)
            .ToList();
      });
   }

   [Fact]
   public void ApplyFiltering_SingleField()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("name=John")
         .ToList();
      var expected = _fakeRepository.Where(q => q.Name == "John").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }


   [Fact]
   public void ApplyFiltering_SingleField_GridifyQuery()
   {
      var gq = new GridifyQuery { Filter = "name=John" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();
      var expected = _fakeRepository.Where(q => q.Name == "John").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_NullHandlingUsingCustomConvertor()
   {
      // create custom mapper
      var gm = new GridifyMapper<TestClass>(q => q.AllowNullSearch = false).GenerateMappings();

      // map any string to related property , also use Client convertor to handle custom scenarios
      gm.AddMap("date", g => g.MyDateTime!, q => (q == "null" ? null : q)!);

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("date=null", gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.MyDateTime == null).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #220
   public void ApplyFiltering_CustomConvertor_ReturningNull_ShouldWork()
   {
      // create custom mapper
      var gm = new GridifyMapper<TestClass>(q => q.AllowNullSearch = true).GenerateMappings();

      // map any string to related property , also use Client convertor to handle custom scenarios
      gm.AddMap("date", g => g.MyDateTime!, q => (q == "null" ? null : q)!);

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("date=null", gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.MyDateTime == null).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_NullHandlingUsingMapper()
   {
      // create custom mapper
      var gm = new GridifyMapper<TestClass>(q => q.AllowNullSearch = true) // default is true
         .GenerateMappings();

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("MyDateTime=null", gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.MyDateTime == null).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_DisableNullHandlingUsingMapper()
   {
      // create custom mapper
      var gm = new GridifyMapper<TestClass>(q => q.AllowNullSearch = false) // default is true
         .GenerateMappings();

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("MyDateTime=null", gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.MyDateTime.ToString() == "null").ToList();
      Assert.Equal(expected, actual);
      Assert.Empty(actual);
   }

   [Fact]
   public void ApplyFiltering_DuplicatefieldName()
   {
      const string gq = "name=John|name=Sara";
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();
      var expected = _fakeRepository.Where(q => (q.Name == "John") | (q.Name == "Sara")).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }


   [Theory]
   [InlineData(@" name =\(LI\,AM\)", "(LI,AM)")]
   [InlineData(@" name =jessi=ca", "jessi=ca")]
   [InlineData(@" name =\\Liam", @"\Liam")]
   [InlineData(@" name =LI \| AM", @"LI | AM")]
   public void ApplyFiltering_EscapeSpecialCharacters(string textFilter, string rawText)
   {
      var gq = new GridifyQuery { Filter = textFilter };
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();
      var expected = _fakeRepository.Where(q => q.Name == rawText).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_ParenthesisQueryWithoutEscapeShouldThrowException()
   {
      var gq = new GridifyQuery { Filter = @"name=(LI,AM)" };
      Action act = () => _fakeRepository.AsQueryable()
         .ApplyFiltering(gq);

      Assert.Throws<GridifyFilteringException>(act);
   }


   [Fact]
   public void ApplyFiltering_SingleGuidField()
   {
      var guidString = "e2cec5dd-208d-4bb5-a852-50008f8ba366";
      var guid = Guid.Parse(guidString);
      var gq = new GridifyQuery { Filter = "myGuid=" + guidString };
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();
      var expected = _fakeRepository.Where(q => q.MyGuid == guid).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_SingleBrokenGuidField()
   {
      var brokenGuidString = "e2cec5dd-208d-4bb5-a852-";
      var gq = new GridifyQuery { Filter = "myGuid=" + brokenGuidString };

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      Assert.False(actual.Any());
   }


   [Fact]
   public void ApplyFiltering_SingleBrokenGuidField_NotEqual()
   {
      var brokenGuidString = "e2cec5dd-208d-4bb5-a852-";
      var gq = new GridifyQuery { Filter = "myGuid!=" + brokenGuidString };

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      Assert.True(actual.Any());
   }


   [Fact]
   public void ApplyFiltering_InvalidFilterExpressionShouldThrowException()
   {
      var gq = new GridifyQuery { Filter = "=guid,d=" };
      Assert.Throws<GridifyFilteringException>(() =>
         _fakeRepository.AsQueryable().ApplyFiltering(gq).ToList());
   }

   [Fact]
   public void ApplyFiltering_InvalidCharacterShouldThrowException()
   {
      var gq = new GridifyQuery { Filter = "@name=ali" };
      Assert.Throws<GridifyFilteringException>(() =>
         _fakeRepository.AsQueryable().ApplyFiltering(gq).ToList());
   }

   [Theory]
   [InlineData(null)]
   [InlineData("")]
   public void ApplyFiltering_NullOrEmptyFilter_ShouldSkip(string? filter)
   {
      var gq = new GridifyQuery();
      if (filter == null)
         gq = null;
      else
         gq.Filter = filter;

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_CustomConvertor()
   {
      const string gq = "name=liam";
      var gm = new GridifyMapper<TestClass>()
         .GenerateMappings()
         .AddMap("name", q => q.Name, q => q.ToUpper()); // using client side Custom convertor

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq, gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name == "LIAM").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_StartsWith()
   {
      const string gq = "name^A";

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name!.StartsWith("A")).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyEverything_EmptyGridifyQuery()
   {
      var gq = new GridifyQuery();

      var actual = _fakeRepository.AsQueryable()
         .ApplyFilteringOrderingPaging(gq)
         .ToList();

      var expected = _fakeRepository.Skip(0).Take(GridifyGlobalConfiguration.DefaultPageSize).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }


   [Fact]
   public void ApplyFiltering_StartsWithOnNotStringsShouldNotThrowError()
   {
      const string gq = "Id^2";

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Id.ToString().StartsWith("2")).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_NotStartsWith()
   {
      const string gq = "name!^A";

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => !q.Name!.StartsWith("A")).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_EndsWith()
   {
      const string gq = "name $ li";

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name!.EndsWith("li")).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_NotEndsWith()
   {
      const string gq = "name !$ i";

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => !q.Name!.EndsWith("i")).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_MultipleCondition()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("name=Jack|name=Rose|id>=7")
         .ToList();
      var expected = _fakeRepository.Where(q => q.Name == "Jack" || q.Name == "Rose" || q.Id >= 7).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_ComplexWithParenthesis()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering("(name=*J|name=*S),(Id<5)")
         .ToList();
      var expected = _fakeRepository.Where(q => (q.Name!.Contains("J") || q.Name.Contains("S")) && q.Id < 5).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_NestedParenthesisWithSpace()
   {
      // we shouldn't add spaces for values
      var gq = new GridifyQuery { Filter = " ( name =*J| ( name =*S , Id <5 ) )" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();
      var expected = _fakeRepository.Where(q => q.Name!.Contains("J") || q.Name.Contains("S") && q.Id < 5).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_UsingChildClassProperty()
   {
      var gq = new GridifyQuery { Filter = "Child_Name=Bob" };
      var gm = new GridifyMapper<TestClass>()
         .GenerateMappings()
         .AddMap("Child_name", q => q.ChildClass!.Name);

      var actual = _fakeRepository.AsQueryable()
         .Where(q => q.ChildClass != null)
         .ApplyFiltering(gq, gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.ChildClass is { Name: "Bob" }).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_CaseInsensitiveSearchUsingConvertor() //issue #21
   {
      var gq = new GridifyQuery { Filter = "name=BOB" };
      var gm = new GridifyMapper<TestClass>()
         .AddMap("name", q => q.Name!.ToLower(), c => c.ToLower());

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq, gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name!.ToLower() == "BOB".ToLower()).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_CaseInsensitiveSearch() //issue #21
   {
      var gq = new GridifyQuery { Filter = "name=BOB/i " };

      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(gq)
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name!.ToLower() == "BOB".ToLower()).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_EscapeCaseInsensitiveSearch() //issue #21
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(@"name=Case\/i")
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name == "Case/i").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_EscapeEscapeCharacter()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(@"Name=Esc/\\pe")
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name == @"Esc/\pe").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact]
   public void ApplyFiltering_CaseInsensitiveOperatorAtTheBeginningOfValue_ShouldIgnore()
   {
      var actual = _fakeRepository.AsQueryable()
         .ApplyFiltering(@"name=\/icase")
         .ToList();

      var expected = _fakeRepository.Where(q => q.Name == "/icase").ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.False(actual.Any());
   }

   [Fact] // issue #27
   public void ApplyFiltering_GreaterThanBetweenTwoStrings()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name > ali").ToList();

      var expected = _fakeRepository.Where(q => string.Compare(q.Name, "ali", StringComparison.Ordinal) > 0).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }


   [Fact] // issue #27
   public void ApplyFiltering_LessThanBetweenTwoStrings()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name < v").ToList();

      var expected = _fakeRepository.Where(q => string.Compare(q.Name, "v", StringComparison.Ordinal) < 0).ToList();
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #27
   public void ApplyFiltering_LessThanOrEqualBetweenTwoStrings()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name <= l").ToList();
      var expected = _fakeRepository.Where(q => string.Compare(q.Name, "l", StringComparison.Ordinal) <= 0).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #27
   public void ApplyFiltering_GreaterThanOrEqualBetweenTwoStrings()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name >= c").ToList();
      var expected = _fakeRepository.Where(q => string.Compare(q.Name, "c", StringComparison.Ordinal) >= 0).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #27
   public void ApplyFiltering_GreaterThanOrEqual_CaseInsensitive_BetweenTwoStrings()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name >= j/i").ToList();
      var expected = _fakeRepository.Where(q => string.Compare(q.Name, "j", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #25
   public void ApplyFiltering_Equal_ProcessingNullOrDefaultValue()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("tag=").ToList();
      var expected = _fakeRepository.Where(q => string.IsNullOrEmpty(q.Tag)).ToList();
      var expected2 = _fakeRepository.Where(q => q.Tag is null or "").ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected2.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.Equal(expected2, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #25
   public void ApplyFiltering_NotEqual_ProcessingNullOrDefaultValue()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("tag!=").ToList();
      var expected = _fakeRepository.Where(q => !string.IsNullOrEmpty(q.Tag)).ToList();
      var expected2 = _fakeRepository.Where(q => q.Tag is not null && q.Tag != "").ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected2.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.Equal(expected2, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #25
   public void ApplyFiltering_Equal_ProcessingNullOrDefaultValueNonStringTypes()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("myGuid=").ToList();
      var expected = _fakeRepository.Where(q => q.MyGuid == default).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #25
   public void ApplyFiltering_NotEqual_ProcessingNullOrDefaultValueNonStringTypes()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("myGuid!=").ToList();
      var expected = _fakeRepository.Where(q => q.MyGuid != default).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #33
   public void ApplyFiltering_WithSpaces()
   {
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name =ali reza").ToList();
      var expected = _fakeRepository.Where(q => q.Name == "ali reza").ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }


   [Fact] // issue #34
   public void ApplyFiltering_UnmappedFields_ShouldThrowException()
   {
      var gm = new GridifyMapper<TestClass>()
         .AddMap("Id", q => q.Id);

      var exp = Assert.Throws<GridifyMapperException>(() => _fakeRepository.AsQueryable().ApplyFiltering("name=John,id>0", gm).ToList());
      Assert.Equal("Mapping 'name' not found", exp.Message);
   }

   [Fact] // issue #34
   public void ApplyFiltering_UnmappedFields_ShouldSkipWhenIgnored()
   {
      var gm = new GridifyMapper<TestClass>(configuration => configuration.IgnoreNotMappedFields = true)
         .AddMap("Id", q => q.Id);

      // name=*a filter should be ignored
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name=*a, id>15", gm).ToList();
      var expected = _fakeRepository.Where(q => q.Id > 15).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #238
   public void ApplyFiltering_MapperDisableCollectionNullChecks_ShouldPreventAddingNullChecks()
   {
      var gm = new GridifyMapper<TestClass>(configuration => configuration.DisableCollectionNullChecks = true)
               .AddMap("name", q => q.Children.Select(w => w.Name));

#pragma warning disable CS8602 // Dereference of a possibly null reference.
      var expected = _fakeRepository.AsQueryable().Where(q => q.Children.Any(w => w.Name.Contains("a"))).ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name=*a", gm).ToString();

      Assert.Equal(expected, actual);
   }

   #endregion


   #region CustomOperator

   internal class UpperCaseEqual : IGridifyOperator
   {
      public string GetOperator()
      {
         return "#=";
      }

      public Expression<OperatorParameter> OperatorHandler()
      {
         return (prop, value) => prop.Equals(value.ToString()!.ToUpper());
      }
   }



   /// <summary>
   /// we need to use this class only once, to avoid concurrency problems
   /// </summary>
   private class TestOperator : UpperCaseEqual
   {
   };

   [Fact]
   [SuppressMessage("Assertions", "xUnit2031:Do not use Where clause with Assert.Single")]
   [SuppressMessage("Assertions", "xUnit2029:Do not use Assert.Empty to check if a value does not exist in a collection")]
   public void CustomOperator_GenericRegisterAndRemove_ShouldNotThrowAnyException()
   {
      GridifyGlobalConfiguration.CustomOperators.Register<TestOperator>();

      Assert.Single(GridifyGlobalConfiguration.CustomOperators.Operators.Where(q => q is TestOperator));

      GridifyGlobalConfiguration.CustomOperators.Remove<TestOperator>();

      Assert.Empty(GridifyGlobalConfiguration.CustomOperators.Operators.Where(q => q is TestOperator));
   }


   [Fact]
   public void ApplyFiltering_CustomOperator()
   {
      var op = new UpperCaseEqual();
      GridifyGlobalConfiguration.CustomOperators.Register(op);
      // Arrange
      var expected = _fakeRepository
         .Where(q => q.Name == "liam".ToUpper())
         .ToList();

      // Act
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name#=liam").ToList();

      // Assert
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());

      // Cleanup
      GridifyGlobalConfiguration.CustomOperators.Remove(op.GetOperator());
   }

   [Fact] // PR #114
   public void ApplyFiltering_WhenPassingBracketsInTheValue_ShouldWorkCorrectly()
   {
      // Arrange
      var expected = _fakeRepository
         .Where(q => q.Name == "[ali]")
         .ToList();

      // Act
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("name=[ali]").ToList();

      // Assert
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #71
   public void ApplyFiltering_BooleanValues_ShouldBeParsedWithZeroAndOne()
   {
      // Arrange
      var expected = _fakeRepository.Where(q => q.IsActive).ToList();

      // Act
      var actual = _fakeRepository.AsQueryable().ApplyFiltering("isActive=1").ToList();

      // Assert
      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   #endregion

   #region "ApplyOrdering"

   [Fact]
   public void ApplyOrdering_OrderBy_Ascending()
   {
      var gq = new GridifyQuery { OrderBy = "name" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository.OrderBy(q => q.Name).ToList();
      Assert.Equal(expected, actual);
   }

   [Fact]
   public void ApplyOrdering_OrderBy_DateTime()
   {
      var gq = new GridifyQuery { OrderBy = "MyDateTime" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository.OrderBy(q => q.MyDateTime).ToList();

      Assert.Equal(expected, actual);
      Assert.Equal(expected.First().Id, actual.First().Id);
   }

   [Fact]
   public void ApplyOrdering_OrderBy_Descending()
   {
      var gq = new GridifyQuery { OrderBy = "Name desc" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository.OrderByDescending(q => q.Name).ToList();

      Assert.Equal(expected, actual);
      Assert.Equal(expected.First().Id, actual.First().Id);
   }

   [Fact]
   public void ApplyOrdering_MultipleOrderBy()
   {
      var gq = new GridifyQuery { OrderBy = "MyDateTime desc , id , name asc" };
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository
         .OrderByDescending(q => q.MyDateTime)
         .ThenBy(q => q.Id)
         .ThenBy(q => q.Name)
         .ToList();

      Assert.Equal(expected.First().Id, actual.First().Id);
      Assert.Equal(expected.Last().Id, actual.Last().Id);
      Assert.Equal(expected, actual);
   }


   [Fact]
   public void ApplyOrdering_SortUsingChildClassProperty()
   {
      var gq = new GridifyQuery { OrderBy = "Child_Name desc" };
      var gm = new GridifyMapper<TestClass>()
         .GenerateMappings()
         .AddMap("Child_Name", q => q.ChildClass!.Name);

      var actual = _fakeRepository.AsQueryable().Where(q => q.ChildClass != null)
         .ApplyOrdering(gq, gm)
         .ToList();

      var expected = _fakeRepository.Where(q => q.ChildClass != null)
         .OrderByDescending(q => q.ChildClass!.Name).ToList();

      Assert.Equal(expected, actual);
   }

   [Fact]
   public void ApplyOrdering_EmptyOrderBy_ShouldSkip()
   {
      var gq = new GridifyQuery();
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository.ToList();
      Assert.Equal(expected, actual);
   }

   [Fact]
   public void ApplyOrdering_NullGridifyQuery_ShouldSkip()
   {
      GridifyQuery? gq = null;
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrdering(gq)
         .ToList();
      var expected = _fakeRepository.ToList();
      Assert.Equal(expected, actual);
   }

   [Fact] // issue #34
   public void ApplyOrdering_UnmappedFields_ShouldThrowException()
   {
      var gm = new GridifyMapper<TestClass>()
         .AddMap("Id", q => q.Id);

      var exp = Assert.Throws<GridifyMapperException>(() => _fakeRepository.AsQueryable().ApplyOrdering("name,id", gm).ToList());
      Assert.Equal("Mapping 'name' not found", exp.Message);
   }

   [Fact] // issue #34
   public void ApplyOrdering_UnmappedFields_ShouldSkipWhenIgnored()
   {
      var gm = new GridifyMapper<TestClass>(configuration => configuration.IgnoreNotMappedFields = true)
         .AddMap("Id", q => q.Id);

      // name orderBy should be ignored
      var actual = _fakeRepository.AsQueryable().ApplyOrdering("name,id", gm).ToList();
      var expected = _fakeRepository.OrderBy(q => q.Id).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #69
   public void ApplyOrdering_NullableTypes_IsNotNull()
   {
      var actual = _fakeRepository.AsQueryable().ApplyOrdering("MyDateTime?").ToList();
      var expected = _fakeRepository.OrderBy(q => q.MyDateTime.HasValue).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Fact] // issue #69
   public void ApplyOrdering_NullableTypes_IsNull()
   {
      var actual = _fakeRepository.AsQueryable().ApplyOrdering("MyDateTime!").ToList();
      var expected = _fakeRepository.OrderBy(q => !q.MyDateTime.HasValue).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   #endregion

   #region "ApplyPaging"

   [Fact]
   public void ApplyPaging_UsingDefaultValues()
   {
      var gq = new GridifyQuery();
      var actual = _fakeRepository.AsQueryable()
         .ApplyPaging(gq)
         .ToList();

      // just returning first page with default size
      var expected = _fakeRepository.Take(GridifyGlobalConfiguration.DefaultPageSize).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
      Assert.True(actual.Any());
   }

   [Theory]
   [InlineData(1, 5)]
   [InlineData(2, 5)]
   [InlineData(1, 10)]
   [InlineData(4, 3)]
   [InlineData(5, 3)]
   [InlineData(1, 15)]
   [InlineData(20, 10)]
   public void ApplyPaging_UsingCustomValues(int page, int pageSize)
   {
      var gq = new GridifyQuery { Page = page, PageSize = pageSize };
      var actual = _fakeRepository.AsQueryable()
         .ApplyPaging(gq)
         .ToList();

      var skip = (page - 1) * pageSize;
      var expected = _fakeRepository.Skip(skip)
         .Take(pageSize).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected, actual);
   }

   #endregion

   #region "Other"

   [Fact]
   public void Gridify_ActionOverload()
   {
      var actual = _fakeRepository.AsQueryable()
         .Gridify(q =>
         {
            q.Filter = "name=John";
            q.PageSize = 13;
            q.OrderBy = "name desc";
         });

      var query = _fakeRepository.AsQueryable().Where(q => q.Name == "John").OrderByDescending(q => q.Name);
      var totalItems = query.Count();
      var items = query.Skip(-2).Take(15).ToList();
      var expected = new Paging<TestClass> { Data = items, Count = totalItems };

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected.Data.Count(), actual.Data.Count());
      Assert.True(actual.Data.Any());
      Assert.Equal(expected.Data.First().Id, actual.Data.First().Id);
   }

   [Theory]
   [InlineData(0, 5, true)]
   [InlineData(1, 5, false)]
   [InlineData(0, 10, true)]
   [InlineData(3, 3, false)]
   [InlineData(4, 3, true)]
   [InlineData(0, 15, false)]
   [InlineData(19, 10, true)]
   public void ApplyOrderingAndPaging_UsingCustomValues(int page, int pageSize, bool isSortAsc)
   {
      var orderByExp = "name " + (isSortAsc ? "asc" : "desc");
      var gq = new GridifyQuery { Page = page, PageSize = pageSize, OrderBy = orderByExp };
      // actual
      var actual = _fakeRepository.AsQueryable()
         .ApplyOrderingAndPaging(gq)
         .ToList();

      // expected
      var skip = (page - 1) * pageSize;
      var expectedQuery = _fakeRepository.AsQueryable();
      if (isSortAsc)
         expectedQuery = expectedQuery.OrderBy(q => q.Name);
      else
         expectedQuery = expectedQuery.OrderByDescending(q => q.Name);
      var expected = expectedQuery.Skip(skip).Take(pageSize).ToList();

      Assert.Equal(expected.Count, actual.Count);
      Assert.Equal(expected.FirstOrDefault()?.Id, actual.FirstOrDefault()?.Id);
      Assert.Equal(expected.LastOrDefault()?.Id, actual.LastOrDefault()?.Id);
   }

   #endregion

   #region Validation

   [Theory]
   [InlineData("notExist=123", false)]
   [InlineData("name=ali", true)]
   [InlineData("name123,123", false)]
   [InlineData("!name<23", false)]
   [InlineData("(id=2,tag=someTag)", true)]
   [InlineData("@name=john", false)]
   //[InlineData("id<nonNumber", false)] // we don't validate runtime values yet.
   public void IsValid_IGridifyFiltering_ShouldReturnExpectedResult(string filter, bool expected)
   {
      var gq = new GridifyQuery { Filter = filter };
      var actual = gq.IsValid<TestClass>();
      Assert.Equal(expected, actual);
   }

   [Theory]
   [InlineData("notExist", false)]
   [InlineData("name", true)]
   [InlineData("name,", false)]
   [InlineData("!name", false)]
   [InlineData("name des", false)]
   [InlineData("id,name", true)]
   [InlineData("id asc,name desc", true)]
   [InlineData("id asc,name2 desc", false)]
   public void IsValid_IGridifyOrdering_ShouldReturnExpectedResult(string order, bool expected)
   {
      var gq = new GridifyQuery { OrderBy = order };
      var actual = gq.IsValid<TestClass>();
      Assert.Equal(expected, actual);
   }

   #endregion
}
