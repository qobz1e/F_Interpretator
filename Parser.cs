using System;
using System.Collections.Generic;
using System.Data.Common;

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

            //// Проверяем специальные формы (только известные ключевые слова)
            //if (firstElement is IdentifierNode identifier && IsSpecialForm(identifier.Name.ToLower()))
            //{
            //    return ParseSpecialForm(identifier.Name.ToLower());
            //}

            //// Проверяем встроенные функции (только известные операторы)
            //if (firstElement is IdentifierNode id && IsBuiltInFunction(id.Name.ToLower()))
            //{
            //    return ParseFunctionCall(id.Name);
            //}

            //// Если первый элемент - лямбда, то это вызов лямбда-функции
            //if (firstElement is LambdaNode lambda)
            //{
            //    return ParseLambdaCall(lambda);
            //}

            // Обычный список
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

        private ASTNode ParseFunctionCall(string functionName)
        {
            var arguments = new List<ASTNode>();
            
            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;
                
                arguments.Add(ParseExpression());
            }
            
            Consume(TokenType.CloseParenthesis);
            return new FunctionCallNode(functionName, arguments);
        }

        private ASTNode ParseSpecialForm(string formName)
        {
            return formName switch
            {
                "quote" => ParseQuote(),
                "setq" => ParseSetq(),
                "cond" => ParseCond(),
                "func" => ParseFunc(),
                "lambda" => ParseLambda(),
                "prog" => ParseProg(),
                "while" => ParseWhile(),
                _ => ParseFunctionCall(formName)
            };
        }

        private ASTNode ParseQuote()
        {
            Consume(TokenType.quote_Token);
            var expression = ParseExpression();
            return new QuoteNode(expression);
        }

        private ASTNode ParseSetq()
        {
            // Уже потребили 'setq' как идентификатор
            var variable = ExpectIdentifier();
            var value = ParseExpression();
            Consume(TokenType.CloseParenthesis);
            return new SetqNode(variable, value);
        }
        private ASTNode ParseCond()
        {
            // Уже потребили 'cond' как идентификатор
            SkipWhitespace();

            var test = ParseExpression();
            var result1 = ParseExpression();
            if (currentToken.tokenType == TokenType.CloseParenthesis)
            {
                Consume(TokenType.CloseParenthesis);
                return new ConditionNode(test, result1);
            } else
            {
                var result2 = ParseExpression();
                Consume(TokenType.CloseParenthesis);
                return new ConditionNode(test, result1, result2);
            }
        }

        private ASTNode ParseFunc()
        {
            // Уже потребили 'func' как идентификатор
            var functionName = ExpectIdentifier();
            var parameters = ParseParameters();
            var bodyExpressions = new List<ASTNode>();

            // Считываем все expressions, которые содержит функция
            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;
                bodyExpressions.Add(ParseExpression());
            }

            Consume(TokenType.CloseParenthesis);

            return new FuncNode(functionName, parameters, bodyExpressions);
        }

        private ASTNode ParseLambdaCall(LambdaNode lambda)
        {
            var arguments = new List<ASTNode>();

            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;

                arguments.Add(ParseExpression());
            }

            Consume(TokenType.CloseParenthesis);
            return new LambdaCallNode(lambda, arguments);
        }

        private ASTNode ParseLambda()
        {
            // Уже потребили 'lambda' как идентификатор
            var parameters = ParseParameters();
            var body = ParseExpression();
            Consume(TokenType.CloseParenthesis);
            return new LambdaNode(parameters, body);
        }

        private ASTNode ParseProg()
        {
            // Уже потребили 'prog' как идентификатор
            var parameters = ParseParameters();
            var expressions = new List<ASTNode>();
            
            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;
                
                expressions.Add(ParseExpression());
            }
            
            Consume(TokenType.CloseParenthesis);
            return new ProgNode(parameters, expressions);
        }

        private ASTNode ParseWhile()
        {
            // Уже потребили 'while' как идентификатор
            var condition = ParseExpression();
            var expressions = new List<ASTNode>();

            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;

                expressions.Add(ParseExpression());
            }

            Consume(TokenType.CloseParenthesis);
            return new WhileNode(condition, expressions);
        }

        private List<string> ParseParameters()
        {
            Consume(TokenType.OpenParenthesis);
            var parameters = new List<string>();
            
            while (currentToken.tokenType != TokenType.CloseParenthesis && currentToken.tokenType != TokenType.EOF)
            {
                SkipWhitespace();
                if (currentToken.tokenType == TokenType.CloseParenthesis) break;
                
                parameters.Add(ExpectIdentifier());
            }
            
            Consume(TokenType.CloseParenthesis);
            return parameters;
        }

        private IntegerNode ParseNumber()
        {
            var value = (int)currentToken.literal;
            Consume(TokenType.Integer);
            return new IntegerNode(value);
        }

        private RealNode ParseReal()
        {
            // Обрабатываем числа с точкой в конце (например "2.")
            double value;
            if (currentToken.literal is string strValue)
            {
                // Если literal это строка (например "2."), парсим её
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

        private IdentifierNode ParseIdentifierOrKeyword()
        {
            string name;
            
            // Получаем имя из литерала токена или из типа токена
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
                // Для ключевых слов преобразуем TokenType в строку
                name = TokenTypeToString(currentToken.tokenType);
            }
            
            Consume(currentToken.tokenType);
            return new IdentifierNode(name);
        }

        // Вспомогательные методы
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

        private string ExpectIdentifier()
        {
            if (currentToken.tokenType != TokenType.Identifier && 
                !IsKeywordToken(currentToken.tokenType) &&
                currentToken.tokenType != TokenType.Boolean)
                throw new SyntaxErrorException($"Expected identifier or keyword, but got {currentToken.tokenType}");
            
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
            return name;
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

        private bool IsKeywordToken(TokenType type) =>
            type == TokenType.plus_Token || type == TokenType.minus_Token || 
            type == TokenType.times_Token || type == TokenType.divide_Token ||
            type == TokenType.setq_Token || type == TokenType.cond_Token ||
            type == TokenType.func_Token || type == TokenType.lambda_Token || 
            type == TokenType.prog_Token || type == TokenType.while_Token;

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

        private bool IsSpecialForm(string name) => name switch
        {
            "quote" or "setq" or "cond" or "func" or "lambda" or "prog" or "while" or
            "return" or "break" => true,
            _ => false
        };

        private bool IsBuiltInFunction(string name)
        {
            return name switch
            {
                "plus" or "minus" or "times" or "divide" or
                "equal" or "nonequal" or "less" or "lesseq" or "greater" or "greatereq" or
                "isint" or "isreal" or "isbool" or "isnull" or "isatom" or "islist" or
                "and" or "or" or "xor" or "not" or
                "head" or "tail" or "cons" or "eval" or "value" => true,
                _ => false
            };
        }
    }

    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException(string message) : base(message) { }
    }
}