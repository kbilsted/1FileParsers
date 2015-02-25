using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

using StatePrinter;
using StatePrinter.Configurations;
using StatePrinter.FieldHarvesters;

namespace MiniParser_ParserCombinators.Test
{
  [TestFixture]
  internal class LexerTest
  {
    [Test]
    public void Lex()
    {
      var cfg = new ActParserCfg();
      Lexer l = new Lexer(cfg.GetLexingTokens(),cfg.LexerTokenPredicate);
      var res = l.Lex("//+ a -> b.c", 1).ToList();

      Assert.AreEqual(4, res.Count);
      Assert.AreEqual("//+", res[0].Id);
      Assert.AreEqual("Name", res[1].Id);
      Assert.AreEqual("a", res[1].Content);
      Assert.AreEqual(1, res[1].Line);
      Assert.AreEqual(5, res[1].Column);

      Assert.AreEqual("->", res[2].Id);
      Assert.AreEqual(7, res[2].Column);

      Assert.AreEqual("Name", res[3].Id);
      Assert.AreEqual("b.c", res[3].Content);
      Assert.AreEqual(10, res[3].Column);
    }

    [Test]
    public void Lex2()
    {
      var cfg = new ActParserCfg();
      Lexer l = new Lexer(cfg.GetLexingTokens(), cfg.LexerTokenPredicate);
      var res = l.Lex("a -> b.c; root -> * ; root ->{a,b}", 1).ToList();

      int i = 0;
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("a", res[i++].Content);
      Assert.AreEqual("->", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("b.c", res[i++].Content);
      Assert.AreEqual(";", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("root", res[i++].Content);
      Assert.AreEqual("->", res[i++].Id);
      Assert.AreEqual("*", res[i++].Id);
      Assert.AreEqual(";", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("root", res[i++].Content);
      Assert.AreEqual("->", res[i++].Id);
      Assert.AreEqual("{", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("a", res[i++].Content);
      Assert.AreEqual(",", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("b", res[i++].Content);
      Assert.AreEqual("}", res[i++].Id);
      Assert.AreEqual(res.Count, i);
    }


    [Test]
    public void LexWikiExamples()
    {

      var cfg = new ActParserCfg();
      Lexer l = new Lexer(cfg.GetLexingTokens(), cfg.LexerTokenPredicate);
      var res = l.Lex("a@>c;", 1).ToList();
      int i = 0;
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("a", res[i++].Content);
      Assert.AreEqual("@>", res[i++].Id);
      Assert.AreEqual("Name", res[i].Id);
      Assert.AreEqual("c", res[i++].Content);
      Assert.AreEqual(";", res[i++].Id);
      Assert.AreEqual(res.Count, i);
    }

    [Test]
    public void LexWikiExamplesStatePrinter()
    {
        var cfg = new ActParserCfg();
        Lexer l = new Lexer(cfg.GetLexingTokens(), cfg.LexerTokenPredicate);

        string expected = @"new List<IdentifiedToken>()
[0] = new IdentifiedToken()
{
    Id = ""Name""
    Content = ""a""
}
[1] = new IdentifiedToken()
{
    Id = ""@>""
    Content = ""@>""
}
[2] = new IdentifiedToken()
{
    Id = ""Name""
    Content = ""c""
}
[3] = new IdentifiedToken()
{
    Id = "";""
    Content = "";""
}
";

        var printer = Get.Printer();
        printer.Configuration.Projectionharvester()
            .Include<IdentifiedToken>(x => x.Id, x => x.Content);

        printer.Assert.PrintIsSame(expected, l.Lex("a@>c;", 1).ToList());
    }

  }

    static class Get {
        public static Stateprinter Printer() {
            var printer = new Stateprinter();
            printer.Configuration.SetAreEqualsMethod (Assert.AreEqual);
            printer.Configuration.Culture = CultureInfo.GetCultureInfo("da-DK");

            return printer;
        }
    }

}