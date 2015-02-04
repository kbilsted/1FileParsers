using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MiniParser_ParserCombinators.Test
{
  public class ActParserCfg : MiniParserConfiguration
  {
    public override List<Token> GetLexingTokens()
    {
      return new List<Token>()
               {
                 new Token("SPACE", @"\s+"), // must be first for efficiency
                 new Token("Fail", "Fail"), // must be before name/fullname
                 new Token("Name", @"\w+(\.\w+)*"), // must be before fullname
                 new Token("->", @"-\>"),
                 new Token("@>", @"@\>"),
                 new Token("?>", @"\?\>"),
                 new Token("//+", @"//\+"),
                 new Token("//-", @"//\-"),
                 new Token("//!", @"//\!"),
                 new Token("//<", @"//\<"),
                 new Token(",", ","),
                 new Token("{", @"\{"),
                 new Token("}", @"\}"),
                 new Token("[", @"\["),
                 new Token("]", @"\]"),
                 new Token("(", @"\("),
                 new Token(")", @"\)"),
                 new Token("*", @"\*"),
                 new Token(";", @";"),
                 new Token(":", @":"),
                 new Token("?", @"\?"),
                 new Token("==", @"=="),
                 new Token("|", @"\|"),
                 new Token("_", "_"),
                 new Token("¤", "¤"),
               };

    }

    public override bool LexerTokenPredicate(IdentifiedToken token)
    {
      return token.Id != "SPACE";
    }

    public override ParserSpec GetGramar()
    {
      var scopeOpen = (ParserSpec) "//+" | "//!";
      var destinations = t("*") | "Name" | seq( mute("{"), "Name", star(mute(","), "Name"), mute("}"));
      var BINDING = ast("->", "Name", mute("->"), destinations);
      var ITERATE = ast("@>", "Name", mute("@>"), destinations);
      var INCLUDE = ast("//<", "//<", "Name");
      var stmt = BINDING | ITERATE;
      var LINE = seq(!scopeOpen, star(stmt, ";")) | "//-" | INCLUDE;
      return LINE;
    }
  }


  [TestFixture]
  internal class ParserTest
  {

    [Test]
    public void CombineTest()
    {
      var res = Parser.Combine((x, y) => new And(x, y), new Epsilon(),new TokenSpec("t"), new TokenSpec("b"));
      Assert.IsTrue(res.GetType() == typeof(And));
      Assert.IsTrue(((And)res).a.GetType() == typeof(Epsilon));
      And b = (And) ((And) res).b;
      Assert.IsTrue(b.GetType() == typeof(And));
      Assert.AreEqual("t", (b.a as TokenSpec).expectedToken.Id);
      Assert.AreEqual("b", (b.b as TokenSpec).expectedToken.Id);
    }

    [Test]
    public void HelperAnd()
    {
      var specA = new Epsilon();
      And specB = specA.And(new TokenSpec("a"), new TokenSpec("b"));
      Or res = specB.Or(new TokenSpec("c"));
      And a = (And) res.a;
      Assert.IsTrue(a.a.GetType() == typeof(Epsilon));
      And ab = (And) a.b;
      Assert.AreEqual("a", (ab.a as TokenSpec).expectedToken.Id);
      TokenSpec b = (TokenSpec) res.b;
      Assert.AreEqual("c", b.expectedToken.Id);

    }

    [Test]
    public void Parse()
    {
      var p = new Parser(new ActParserCfg()); 
      var results = p.ParseLine("//+ a->c;", 1);
      PrintParseResult(results);
      
      results = p.ParseLine("//+ a->*;", 1);
      PrintParseResult(results);
      
      results = p.ParseLine("//+ a->{b,c};", 1);
      PrintParseResult(results);

      results = p.ParseLine("//+ a@>c;", 1);
      PrintParseResult(results);
    }

    [Test]
    public void ParseStar()
    {
      Console.WriteLine("ØØØØØØØØØØØØØØØØØØØØØØ");
      var p = new Parser(new ActParserCfg());
      var results = p.ParseLine("//+ a->b;b->c;", 1);
      Assert.IsTrue(results[0].Success);
      Assert.AreEqual(1, results.Count);
      Assert.AreEqual("//+", ((Leaf)results[0].Ast[0]).Token.Id);
      Assert.AreEqual("->", ((Structure)results[0].Ast[1]).Id);

      results = p.ParseLine("//+ a->b;b->c;c->d;d->e;", 1);
      PrintParseResult(results);

      //results = p.ParseLine("//+ a->b;b->c;c->d;d->e", 1);
      //PrintParseResult(results);
      Console.WriteLine("ØØØØØØØØØØØØØØØØØØØØØØ");

    }


    [Test]
    public void ParseFail_missing_endtoken()
    {
      var p = new Parser(new ActParserCfg());
      var results = p.ParseLine("//+ a->c", 1);
      PrintParseResult(results);
    }

    private static void PrintParseResult(List<ParserResult> results)
    {
      int r = 0;
      foreach (var parserResult in results)
      {
        Console.WriteLine(++r);
        Console.WriteLine(parserResult.Success);
        int i = 0;
        foreach (var ast in parserResult.Ast)
        {
          Console.WriteLine(""+r+"."+ ++i );
          Console.WriteLine(ast);
        }
        if (!parserResult.Success)
        {
          Console.WriteLine("expected: {0}", parserResult.Err.ErrorExpectedToken);
          Console.WriteLine("actual: {0} ({1}) column {2}", parserResult.Err.ActualToken.Content, parserResult.Err.ActualToken.Id, parserResult.Err.ActualToken.Column);
        }
      }
    }
  }
}