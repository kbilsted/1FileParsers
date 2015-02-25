using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniParser_ParserCombinators {

  public class Token {
    public readonly string Id;
    public readonly Regex Lookup;

    public Token(string id, string regex) {
      if (id == null) throw new ArgumentNullException("id");
      Id = id;
      Lookup = new Regex("^" + regex);
    }

    public override string ToString() {
      return Id;
    }
  }

  public class IdentifiedToken {
    public readonly int Line, Column;
    public readonly string Content;
    public readonly Token Meta;

    public IdentifiedToken(Token t, string value, int lineno, int column) {
      if (t == null) throw new ArgumentNullException("t");
      if (value == null) throw new ArgumentNullException("value");
      if (lineno < 0) throw new ArgumentOutOfRangeException("lineno");
      if (column < 0) throw new ArgumentOutOfRangeException("column");
      Line = lineno;
      Column = column;
      Content = value;
      Meta = t;
    }

    public string Id { get { return Meta.Id; } }

    public override string ToString() {
      return string.Format("{0} ({1});{2}:{3}", Meta.Id, Content, Line, Column);
    }
  }

  public class Lexer {
    internal readonly List<Token> Configuration;
    public static Token EOF { get { return new Token("END-OF-INPUT", ""); } }

    readonly Func<IdentifiedToken, bool> predicate;

    public Lexer(List<Token> cfg, Func<IdentifiedToken, bool> predicate = null) {
      Configuration = cfg.ToList();
      this.predicate = predicate;
    }

    public IEnumerable<IdentifiedToken> Lex(string line, int lineno) {
      if (line == null) throw new ArgumentNullException("line");
      var allTokens = LexLine(line, lineno);
      if (predicate != null) return allTokens.Where(predicate);
      return allTokens;
    }

    IEnumerable<IdentifiedToken> LexLine(string line, int lineno) {
      int column = 1;
      var result = new List<IdentifiedToken>();

      while (line.Length > 0) {
        string line1 = line;
        Tuple<Token, Match> match =
          Configuration.Select(x => Tuple.Create(x, x.Lookup.Match(line1)))
            .FirstOrDefault(x => x.Item2.Success);
        if (match == default(Tuple<Token, Match>)) throw new Exception("Cannot lex input '" + line + "'");

        var value = match.Item2.Value;
        if (value.Length == 0) throw new Exception("Cannot lex input '" + line + "'");
        result.Add(new IdentifiedToken(match.Item1, value, lineno, column));
        column += value.Length;
        line = line.Substring(value.Length);
      }

      return result;
    }
  }


  public class Parser {
    Lexer lexer;
    ParserSpec root;

    public Parser(MiniParserConfiguration conf) {
      if (conf == null) throw new ArgumentNullException("conf");
      lexer = new Lexer(conf.GetLexingTokens(), conf.LexerTokenPredicate);
      root = conf.GetGramar();
      ValidateGramar();
    }

    internal void ValidateGramar() {
      var notFound =
        root.GetTokenSpecs()
          .Select(x => x.expectedToken.Id)
          .Distinct()
          .Where(x => !lexer.Configuration.Any(c => c.Id == x))
          .ToList();
      if (notFound.Any())
        throw new ArgumentException(
          "The following tokens were found in the grammar but not configured in the lexer: '",
          string.Join(", '", notFound) + "'");
    }

    public List<ParserResult> ParseLine(string line, int lineno) {
      if (line == null) throw new ArgumentNullException("line");
      if (lineno < 0) throw new ArgumentOutOfRangeException("lineno");

      var tokens = lexer.Lex(line, lineno).ToList();
      var results = root.Parse(tokens, 0).ToList();
      var successRes = results.FirstOrDefault(x => x.Success && x.NewPos == tokens.Count);
      if (successRes != null) return new List<ParserResult>() { successRes };
      var fails = results.Where(x => !x.Success).ToList();
      var parsedTheMost = fails.Max(x => x.NewPos);
      return fails.Where(x => x.NewPos == parsedTheMost).ToList();
    }

    internal static ParserSpec Combine<T>(Func<ParserSpec, ParserSpec, T> New, params ParserSpec[] specs) where T : ParserSpec {
      if (New == null) throw new ArgumentNullException("New");
      if (specs == null) throw new ArgumentNullException("specs");
      if (specs.Length == 0) throw new ArgumentException("Must have at least one spec!");
      if (specs.Length == 1) return specs[0];
      T last = New(specs[specs.Length - 2], specs[specs.Length - 1]);
      for (int i = specs.Length - 3; i >= 0; i--) last = New(specs[i], last);
      return last;
    }
  }

  public abstract class MiniParserConfiguration {
    public abstract List<Token> GetLexingTokens();
    public abstract bool LexerTokenPredicate(IdentifiedToken token);
    public abstract ParserSpec GetGramar();

    protected ParserSpec and(params ParserSpec[] specs) { return Parser.Combine((x, y) => new And(x, y), specs); }
    protected ParserSpec seq(params ParserSpec[] specs) { return Parser.Combine((x, y) => new And(x, y), specs); }
    protected ParserSpec or(params ParserSpec[] specs) { return Parser.Combine((x, y) => new Or(x, y), specs); }
    protected Ast ast(string astNodeName, params ParserSpec[] specs) { return new Ast(astNodeName, specs); }
    protected Optional optional(params ParserSpec[] specs) { return new Optional(Parser.Combine((x, y) => new And(x, y), specs)); }
    protected TokenSpec t(string tokenId) { return new TokenSpec(tokenId); }
    public Mute mute(ParserSpec spec) { return new Mute(spec);  }
    protected Star star(params ParserSpec[] specs) { return new Star(Parser.Combine((x, y) => new And(x, y), specs)); }
  }

  #region parserspec

  public abstract class ParserSpec {
    public abstract IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current);
    internal abstract IEnumerable<TokenSpec> GetTokenSpecs();
    protected bool Stop(List<IdentifiedToken> specs, int current) { return specs.Count == current; }

    /// <summary> only return failed parsing if we went further than any other parsing attempt from this node</summary>
    int maxErrorPosReturned = -1;

    /// <summary> only return failed parsing if we went further than any other parsing attempt from this node</summary>
    protected bool ShouldReturnFail(ParserResult result) {
      if (maxErrorPosReturned > result.NewPos) return false;
      maxErrorPosReturned = result.NewPos;
      return true;
    }

    #region navigtion helpers
    public static implicit operator ParserSpec(string t) { return new TokenSpec(t); }
    public static ParserSpec operator |(ParserSpec a, ParserSpec b) { return new Or(a, b); }
    public static ParserSpec operator |(string a, ParserSpec b) { return new Or(new TokenSpec(a), b); }
    public static ParserSpec operator |(ParserSpec a, string b) { return new Or(a, new TokenSpec(b)); }
    public static ParserSpec operator !(ParserSpec a) { return new Mute(a); }
    public Or Or(params ParserSpec[] specSequence) { return new Or(this, Parser.Combine((x, y) => new And(x, y), specSequence)); }
    public And And(params ParserSpec[] specSequence) { return new And(this, Parser.Combine((x, y) => new And(x, y), specSequence)); }
    #endregion
  }


  public class ParserResult {
    public List<AbstractSyntaxTree> Ast = new List<AbstractSyntaxTree>();
    public int NewPos;
    public Error Err;

    public bool Success { get { return Err == null; } }

    public ParserResult(int newPos) {
      NewPos = newPos;
    }

    public ParserResult(int newPos, Error error, params IEnumerable<AbstractSyntaxTree>[] trees) {
      NewPos = newPos;
      Err = error;
      foreach (var t in trees) Ast.AddRange(t);
    }

    public override string ToString() { return (Success ? "T" : "F") + NewPos; }

    public class Error {
      public readonly string ErrorExpectedToken;
      public readonly IdentifiedToken ActualToken;

      public Error(string errorExpectedToken, IdentifiedToken actualToken) {
        ErrorExpectedToken = errorExpectedToken;
        ActualToken = actualToken;
      }
    }
  }

  public abstract class BinarySpec : ParserSpec {
    internal readonly ParserSpec a, b;

    internal BinarySpec(ParserSpec a, ParserSpec b) {
      this.a = a;
      this.b = b;
    }

    internal override IEnumerable<TokenSpec> GetTokenSpecs() {
      return a.GetTokenSpecs().Concat(b.GetTokenSpecs());
    }
  }

  public class And : BinarySpec {
    public And(ParserSpec a, ParserSpec b) : base(a, b) {}

    public override IEnumerable<ParserResult> Parse(
      List<IdentifiedToken> tokens,
      int current) {
      var achild = a.Parse(tokens, current).ToList();
      foreach (var solution1 in achild) {
        if (solution1.Success) {
          var bchild = b.Parse(tokens, solution1.NewPos).ToList();
          foreach (var solution2 in bchild) {
            if (solution2.Success) {
              var result = new ParserResult(
                solution2.NewPos,
                null,
                solution1.Ast,
                solution2.Ast);
              yield return result;

              if (Stop(tokens, result.NewPos)) yield break;
            }
            else {
              if (ShouldReturnFail(solution2)) yield return solution2;
            }
          }
        }
        else {
          if (ShouldReturnFail(solution1)) yield return solution1;
        }
      }
    }
  }


  public class Or : BinarySpec {
    public Or(ParserSpec a, ParserSpec b) : base(a, b) {}

    public override IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current) {
      var achild = a.Parse(tokens, current).ToList();
      var bchild = b.Parse(tokens, current).ToList();
      foreach (var result in achild.Concat(bchild)) {
        if (!result.Success && ShouldReturnFail(result)) yield return result;
        else yield return result;

        if (Stop(tokens, result.NewPos)) 
          yield break;
      }
    }
  }

  /// <summary>always true, does not eat any tokens</summary>
  class Epsilon : ParserSpec {
    public override IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current) {
      yield return new ParserResult(current);
    }

    internal override IEnumerable<TokenSpec> GetTokenSpecs() {
      return Enumerable.Empty<TokenSpec>();
    }
  }


  public class TokenSpec : ParserSpec {
    internal readonly Token expectedToken;

    public TokenSpec(string tokenId) {
      expectedToken = new Token(tokenId, null);
    }

    public override IEnumerable<ParserResult> Parse(
      List<IdentifiedToken> tokens,
      int current) {
      if (current >= tokens.Count)
        return new[] {
                       new ParserResult(
                         current,
                         new ParserResult.Error(
                         expectedToken.Id,
                         new IdentifiedToken(
                         Lexer.EOF,
                         "EOF",
                         tokens[current - 1].Line,
                         tokens[current - 1].Column)))
                     };
      var actual = tokens[current].Id;
      if (actual == expectedToken.Id)
        return new[] {
                       new ParserResult(current + 1, null, new[] { new Leaf(tokens[current]) })
                     };
      return new[] {
                     new ParserResult(
                       current,
                       new ParserResult.Error(expectedToken.Id, tokens[current]))
                   };
    }

    public override string ToString() { return expectedToken.Id; }
    
    internal override IEnumerable<TokenSpec> GetTokenSpecs() { yield return this; }
  }

  public abstract class UnarySpec : ParserSpec {
    internal readonly ParserSpec Inner;

    internal UnarySpec(ParserSpec inner) {
      Inner = inner;
    }

    internal override IEnumerable<TokenSpec> GetTokenSpecs() {
      return Inner.GetTokenSpecs();
    }
  }

  /// <summary>  create an explicit node in the resulting AST</summary>
  public class Ast : UnarySpec {
    string AstNodeName;

    public Ast(string astNodeName, params ParserSpec[] specList) : base(Parser.Combine((x, y) => new And(x, y), specList)) {
      AstNodeName = astNodeName;
    }

    public override IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current) {
      var res = Inner.Parse(tokens, current).ToList();
      foreach (var solution in res) {
        if (solution.Success) {
          solution.Ast = new List<AbstractSyntaxTree>() {
                                                          new Structure(
                                                            AstNodeName,
                                                            solution.Ast)
                                                        };
        }
        yield return solution;
        if (Stop(tokens, solution.NewPos)) 
          yield break;
      }
    }

    public override string ToString() { return "NODE: " + AstNodeName; }
  }

  /// <summary> Mute so that the production from parsing does not appear in the resulting AST.</summary>
  public class Mute : UnarySpec {
    public Mute(ParserSpec spec) : base(spec) {}

    public override IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current) {
      foreach (var result in Inner.Parse(tokens, current)) {
        result.Ast.Clear();
        yield return result;
      }
    }
  }

  public class Optional : UnarySpec {
    public Optional(ParserSpec spec)
      : base(spec) {}

    public override IEnumerable<ParserResult> Parse(
      List<IdentifiedToken> tokens,
      int current) {
      yield return new ParserResult(current); // zero-match
      var solutions = Inner.Parse(tokens, current).ToList();
      foreach (var x in solutions) yield return x;
    }
  }

  public class Star : UnarySpec { 
    public Star(ParserSpec spec) : base(spec) {}

    public override IEnumerable<ParserResult> Parse(List<IdentifiedToken> tokens, int current) {
      yield return new ParserResult(current); // zero-match
      foreach (var x in More(tokens, current, new List<AbstractSyntaxTree>())) yield return x;
    }

    IEnumerable<ParserResult> More(List<IdentifiedToken> tokens, int current, List<AbstractSyntaxTree> ast) {
      var solutions = Inner.Parse(tokens, current).ToList();
      foreach (var solution in solutions) {
        if (!solution.Success) {
          yield return solution;
        }
        else {
          var mergedAst = ast.Concat(solution.Ast).ToList();
          yield return new ParserResult(solution.NewPos, null, mergedAst);
          foreach (var x in More(tokens, solution.NewPos, mergedAst))
            yield return x;
        }

        if (Stop(tokens, solution.NewPos)) yield break;
      }
    }
  }
  #endregion

  #region AbstractSyntaxTree
  public abstract class AbstractSyntaxTree {
    public List<AbstractSyntaxTree> Children = new List<AbstractSyntaxTree>();

    public void Add(AbstractSyntaxTree abstractSyntaxTree) {
      Children.Add(abstractSyntaxTree);
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
      Print(sb, "");
      return sb.ToString();
    }

    internal abstract void Print(StringBuilder sb, string indent);
  }


  class Structure : AbstractSyntaxTree {
    public readonly string Id;
    public readonly List<AbstractSyntaxTree> Ast;

    public Structure(string id, List<AbstractSyntaxTree> ast) {
      if (id == null) throw new ArgumentNullException("id");
      if (ast == null) throw new ArgumentNullException("ast");
      Id = id;
      Ast = ast;
    }

    internal override void Print(StringBuilder sb, string indent) {
      sb.AppendLine(indent + " > " + Id);
      foreach (AbstractSyntaxTree ast in Ast) 
        ast.Print(sb, indent + " > >");
    }
  }


  class Leaf : AbstractSyntaxTree {
    public readonly IdentifiedToken Token;

    public Leaf(IdentifiedToken token) { Token = token; }

    internal override void Print(StringBuilder sb, string indent) {
      sb.AppendLine(indent + " - " + Token.Id + " " + Token.Content);
    }
  }
  #endregion
}