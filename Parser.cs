namespace F_Interpretator
{
    public class Parser
    {
        private Lexer lexer;
        private Token currentToken;

        public Parser(Lexer lexer)
        {
            this.lexer = lexer;
            this.currentToken = lexer.Lex();
            SkipWhitespace();
        }

        public ProgramNode Parse()
        {
            var expressions = new List<ASTNode>();

            while (currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.EOF) break;

                expressions.Add(ParseExpression());
                SkipWhitespace();
            }

            return new ProgramNode(expressions);
        }

        private ASTNode ParseExpression()
        {
            SkipWhitespace();
            return currentToken.tokenType switch
            {
                TokenType.OpenParenthesis => ParseList(),
                TokenType.quote_Token => ParseQuote(),
                _ => ParseAtom()
            };
        }

        private ASTNode ParseAtom()
        {
            SkipWhitespace();
            return currentToken.tokenType switch
            {
                TokenType.Integer => ParseNumber(),
                TokenType.Real => ParseReal(),
                TokenType.Boolean => ParseBoolean(),
                TokenType.Null => ParseNull(),
                TokenType.Identifier or
                TokenType.plus_Token or TokenType.minus_Token or TokenType.times_Token or TokenType.divide_Token or
                TokenType.equal_Token or TokenType.nonequal_Token or TokenType.less_Token or TokenType.lesseq_Token or TokenType.greater_Token or TokenType.greatereq_Token or
                TokenType.isint_Token or TokenType.isreal_Token or TokenType.isbool_Token or TokenType.isnull_Token or
                TokenType.isatom_Token or TokenType.islist_Token or
                TokenType.and_Token or TokenType.or_Token or TokenType.xor_Token or TokenType.not_Token or
                TokenType.head_Token or TokenType.tail_Token or TokenType.cons_Token or
                TokenType.eval_Token or TokenType.setq_Token or TokenType.func_Token or TokenType.lambda_Token or
                TokenType.prog_Token or TokenType.cond_Token or TokenType.while_Token or TokenType.return_Token or
                TokenType.break_Token or TokenType.value_Token => ParseIdentifierOrKeyword(),
                _ => throw new SyntaxErrorException($"Unexpected token: {currentToken}")
            };
        }

        private ASTNode ParseList()
        {
            Consume(TokenType.OpenParenthesis);
            SkipWhitespace();

            if (currentToken.tokenType == TokenType.CloseParenthesis)
            {
                Consume(TokenType.CloseParenthesis);
                return new ListNode(new List<ASTNode>());
            }

            var firstElement = ParseExpression();

            var elements = new List<ASTNode> { firstElement };
            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;

                elements.Add(ParseExpression());
            }

            Consume(TokenType.CloseParenthesis);
            return new ListNode(elements);
        }


        private ASTNode ParseQuote()
        {
            Consume(TokenType.quote_Token);
            var expression = ParseExpression();
            return new QuoteNode(expression);
        }

        private IntegerNode ParseNumber()
        {
            var value = (int)currentToken.literal;
            Consume(TokenType.Integer);
            return new IntegerNode(value);
        }

        private RealNode ParseReal()
        {
            double value;
            if (currentToken.literal is string strValue)
            {
                if (double.TryParse(strValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedValue))
                {
                    value = parsedValue;
                }
                else
                {
                    throw new SyntaxErrorException($"Invalid real number: {strValue}");
                }
            }
            else
            {
                value = (double)currentToken.literal;
            }

            Consume(TokenType.Real);
            return new RealNode(value);
        }

        private BooleanNode ParseBoolean()
        {
            var value = currentToken.literal is bool boolValue ? boolValue :
                       currentToken.literal?.ToString()?.ToLower() == "true";
            Consume(TokenType.Boolean);
            return new BooleanNode(value);
        }

        private NullNode ParseNull()
        {
            Consume(TokenType.Null);
            return NullNode.Instance;
        }

        private IdentifierNode ParseIdentifierOrKeyword()
        {
            string name;

            if (currentToken.tokenType == TokenType.Identifier)
            {
                name = (string)currentToken.literal;
            }
            else if (currentToken.tokenType == TokenType.Boolean)
            {
                name = currentToken.literal?.ToString() ?? "false";
            }
            else
            {
                name = TokenTypeToString(currentToken.tokenType);
            }

            Consume(currentToken.tokenType);
            return new IdentifierNode(name);
        }

        private void Consume(TokenType expectedType)
        {
            if (currentToken.tokenType != expectedType && currentToken.tokenType != TokenType.EndLine)
                throw new SyntaxErrorException($"Expected {expectedType}, but got {currentToken.tokenType}");

            if (currentToken.tokenType != TokenType.EOF)
            {
                currentToken = lexer.Lex();
                SkipWhitespace();
            }
        }

        private void SkipWhitespace()
        {
            while (currentToken.tokenType == TokenType.EndLine ||
                   currentToken.tokenType == TokenType.EOF)
            {
                if (currentToken.tokenType == TokenType.EOF) break;
                currentToken = lexer.Lex();
            }
        }

        private string TokenTypeToString(TokenType type)
        {
            return type switch
            {
                TokenType.plus_Token => "plus",
                TokenType.minus_Token => "minus",
                TokenType.times_Token => "times",
                TokenType.divide_Token => "divide",
                TokenType.setq_Token => "setq",
                TokenType.cond_Token => "cond",
                TokenType.func_Token => "func",
                TokenType.lambda_Token => "lambda",
                TokenType.prog_Token => "prog",
                TokenType.while_Token => "while",
                TokenType.and_Token => "and",
                TokenType.or_Token => "or",
                TokenType.xor_Token => "xor",
                TokenType.not_Token => "not",
                _ => type.ToString().ToLower().Replace("_token", "")
            };
        }
    }

    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException(string message) : base(message) { }
    }
}