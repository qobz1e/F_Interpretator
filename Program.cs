using F_Interpretator;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

internal class Program
{
    private static void Main(string[] args)
    {
        var examplesFolder = @"C:\Users\thatm\F_Interpretator\examples";

        foreach (var filePath in Directory.GetFiles(examplesFolder, "*.fl").OrderBy(f => f))
        {
            try
            {
                var source = File.ReadAllText(filePath);
                Console.WriteLine($"=== Parsing: {Path.GetFileName(filePath)} ===");
                Console.WriteLine(source);
                Console.WriteLine("--- AST ---");

                var lexer = new Lexer(source);
                var parser = new Parser(lexer);

                var program = parser.Parse();
                PrintAST(program, 0);

                Console.WriteLine("=====================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing {Path.GetFileName(filePath)}: {ex.Message}");
                Console.WriteLine("=====================\n");
            }
        }

        Console.ReadKey();
    }

    private static void PrintAST(ASTNode node, int indent)
    {
        var indentStr = new string(' ', indent * 2);

        switch (node)
        {
            case ProgramNode program:
                Console.WriteLine($"{indentStr}Program");
                foreach (var expr in program.Expressions)
                    PrintAST(expr, indent + 1);
                break;
            case IntegerNode num:
                Console.WriteLine($"{indentStr}Integer: {num.Value}");
                break;
            case RealNode real:
                Console.WriteLine($"{indentStr}Real: {real.Value.ToString(CultureInfo.InvariantCulture)}");
                break;
            case IdentifierNode id:
                Console.WriteLine($"{indentStr}Identifier: {id.Name}");
                break;
            case BooleanNode boolNode:
                Console.WriteLine($"{indentStr}Boolean: {boolNode.Value}");
                break;
            case ListNode list:
                Console.WriteLine($"{indentStr}List");
                foreach (var element in list.Elements)
                    PrintAST(element, indent + 1);
                break;
            case FunctionCallNode func:
                Console.WriteLine($"{indentStr}FunctionCall: {func.FunctionName}");
                foreach (var arg in func.Arguments)
                    PrintAST(arg, indent + 1);
                break;
            case QuoteNode quote:
                Console.WriteLine($"{indentStr}Quote");
                PrintAST(quote.Expression, indent + 1);
                break;
            case SetqNode setq:
                Console.WriteLine($"{indentStr}Setq: {setq.VariableName}");
                PrintAST(setq.Value, indent + 1);
                break;
            case ConditionNode cond:
                Console.WriteLine($"{indentStr}Cond");
                PrintAST(cond.Test, indent + 1);
                Console.WriteLine($"{indentStr}  case true ->");
                PrintAST(cond.ResultTrue, indent + 2);
                if (cond.ResultFalse != null)
                {
                    Console.WriteLine($"{indentStr}  case false ->");
                    PrintAST(cond.ResultFalse, indent + 2);
                }
                break;
            case FuncNode func:
                Console.WriteLine($"{indentStr}Func: {func.FunctionName}({string.Join(", ", func.Parameters)})");
                foreach (var exp in func.Expressions)
                    PrintAST(exp, indent + 1);
                break;
            case ProgNode prog:
                Console.WriteLine($"{indentStr}Prog ({string.Join(", ", prog.Parameters)})");
                foreach (var exp in prog.Expressions)
                    PrintAST(exp, indent + 1);
                break;
            case WhileNode While:
                Console.WriteLine($"{indentStr}While");
                PrintAST(While.Condition, indent + 1);
                foreach (var exp in While.Expressions)
                    PrintAST(exp, indent + 2);
                break;
            case LambdaNode lambda:
                Console.WriteLine($"{indentStr}Lambda({string.Join(", ", lambda.Parameters)})");
                PrintAST(lambda.Body, indent + 1);
                break;
            case LambdaCallNode lambdaCall:
                Console.WriteLine($"{indentStr}LambdaCall");
                PrintAST(lambdaCall.Lambda, indent + 1);
                Console.WriteLine($"{indentStr}  Arguments:");
                foreach (var arg in lambdaCall.Arguments)
                    PrintAST(arg, indent + 2);
                break;
            default:
                Console.WriteLine($"{indentStr}{node.GetType().Name}");
                break;
        }
    }
}