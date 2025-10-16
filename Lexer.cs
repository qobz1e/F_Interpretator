using System.Globalization;

namespace F_Interpretator
{
    public class Lexer
    {
        public List<Token> tokens { get; set; }
        public string source_string { get; }
        private char curChar;
        private char nextChar;
        private int pos;

        public Lexer(string source)
        {
            source_string = source ?? throw new ArgumentNullException(nameof(source));
            tokens = new List<Token>();

            pos = -2;

            curChar = '\0';
            nextChar = '\0';

            Next();
        }

        public void ConsumeWhiteSpace()
        {
            while (curChar != '\0' && char.IsWhiteSpace(curChar))
            {
                Next();
            }
        }

        public void Next()
        {
            pos++;
            curChar = nextChar;

            if (pos <= (source_string.Length - 2))
            {
                nextChar = source_string[pos + 1];
            }
            else
            {
                nextChar = '\0';
            }
        }

        public Token Lex()
        {
            var token = new Token(TokenType.EOF, "\0");
            Next();
            ConsumeWhiteSpace();
            var currentChar = curChar;
            var charString = currentChar.ToString();

            if (currentChar == '\n' | currentChar == '\r')
            {
                return new Token(TokenType.EndLine, charString);
            }

            if (currentChar == '\0')
            {
                token = new Token(TokenType.EOF, charString);
                //_Tokens.Add(token);
                return token;
            }
            else if (currentChar == '\'')
            {
                return new Token(TokenType.quote_Token, charString);
            }
            else if (currentChar == '(')
            {
                return new Token(TokenType.OpenParenthesis, charString);
            }
            else if (currentChar == ')')
            {
                return new Token(TokenType.CloseParenthesis, charString);
            }
            else if (char.IsLetter(currentChar)) //possible start of a reserved keyWord - var
            {
                var prevToken = MakeIdentifierLiteral();

                if (FindKeyword(prevToken?.literal?.ToString() ?? string.Empty, out var tokenType))
                {
                    return new Token(tokenType, prevToken?.literal?.ToString() ?? string.Empty);
                }

                return new Token(TokenType.Identifier, prevToken?.literal?.ToString() ?? string.Empty);

            }
            else if (char.IsDigit(currentChar) || (currentChar == '-' && char.IsDigit(nextChar)) || (currentChar == '.' && char.IsDigit(nextChar)))
            {
                return MakeLiteral();
            }

            return new Token(TokenType.EOF, "\0");
        }

        private bool FindKeyword(string keyword, out TokenType tokenType)
        {
            switch (keyword.ToLowerInvariant())
            {
                case "plus":
                    tokenType = TokenType.plus_Token;
                    break;
                case "minus":
                    tokenType = TokenType.minus_Token;
                    break;
                case "times":
                    tokenType = TokenType.times_Token;
                    break;
                case "divide":
                    tokenType = TokenType.divide_Token;
                    break;

                case "true":
                    tokenType = TokenType.Boolean;
                    break;
                case "false":
                    tokenType = TokenType.Boolean;
                    break;

                case "equal":
                    tokenType = TokenType.equal_Token;
                    break;
                case "nonequal":
                    tokenType = TokenType.nonequal_Token;
                    break;
                case "less":
                    tokenType = TokenType.less_Token;
                    break;
                case "lesseq":
                    tokenType = TokenType.lesseq_Token;
                    break;
                case "greater":
                    tokenType = TokenType.greater_Token;
                    break;
                case "greatereq":
                    tokenType = TokenType.greatereq_Token;
                    break;

                case "isint":
                    tokenType = TokenType.isint_Token;
                    break;
                case "isreal":
                    tokenType = TokenType.isreal_Token;
                    break;
                case "isbool":
                    tokenType = TokenType.isbool_Token;
                    break;
                case "isnull":
                    tokenType = TokenType.isnull_Token;
                    break;
                case "isatom":
                    tokenType = TokenType.isatom_Token;
                    break;
                case "islist":
                    tokenType = TokenType.islist_Token;
                    break;

                case "and":
                    tokenType = TokenType.and_Token;
                    break;
                case "or":
                    tokenType = TokenType.or_Token;
                    break;
                case "xor":
                    tokenType = TokenType.xor_Token;
                    break;
                case "not":
                    tokenType = TokenType.not_Token;
                    break;

                case "head":
                    tokenType = TokenType.head_Token;
                    break;
                case "tail":
                    tokenType = TokenType.tail_Token;
                    break;
                case "cons":
                    tokenType = TokenType.cons_Token;
                    break;

                case "eval":
                    tokenType = TokenType.eval_Token;
                    break;

                case "quote":
                    tokenType = TokenType.quote_Token;
                    break;
                case "setq":
                    tokenType = TokenType.setq_Token;
                    break;
                case "func":
                    tokenType = TokenType.func_Token;
                    break;
                case "lambda":
                    tokenType = TokenType.lambda_Token;
                    break;
                case "prog":
                    tokenType = TokenType.prog_Token;
                    break;
                case "cond":
                    tokenType = TokenType.cond_Token;
                    break;
                case "while":
                    tokenType = TokenType.while_Token;
                    break;
                case "return":
                    tokenType = TokenType.return_Token;
                    break;
                case "break":
                    tokenType = TokenType.break_Token;
                    break;

                default:
                    tokenType = TokenType.EOF;
                    break;
            }

            if (tokenType != TokenType.EOF)
            {
                return true;
            }
            return false;
        }

        private Token MakeLiteral()
        {
            var currentPos = pos;
            var dots = 0;
            while (curChar != '\0' && "0123456789.".Contains(nextChar))
            {
                if (curChar == '.')
                {
                    dots++;
                }
                Next();
            }
            if (curChar == '.')
            {
                dots++;
            }
            if (dots > 1) throw new FormatException("Invalid number format");
            var numValue = source_string.Substring(currentPos, pos - currentPos + 1);
            if (dots == 1)
            {
                return new Token(TokenType.Real, double.Parse(numValue, CultureInfo.InvariantCulture));
            }
            else
            {
                return new Token(TokenType.Integer, int.Parse(numValue, CultureInfo.InvariantCulture));
            }

        }

        private Token MakeIdentifierLiteral()
        {
            var currentPos = pos;
            while (curChar != '\0' && curChar != '\n' && (char.IsLetter(nextChar) || char.IsDigit(nextChar)))
            {
                Next();
            }

            var literal = source_string.Substring(currentPos, pos - currentPos + 1);

            return new Token(TokenType.Identifier, literal);
        }
    }
}