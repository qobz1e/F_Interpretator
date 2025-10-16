using System.Collections.Generic;
using System.Linq.Expressions;

namespace F_Interpretator
{
    public abstract class ASTNode { }

    public class ProgramNode : ASTNode
    {
        public List<ASTNode> Expressions { get; }
        public ProgramNode(List<ASTNode> expressions) => Expressions = expressions;
    }

    public class IntegerNode : ASTNode
    {
        public int Value { get; }
        public IntegerNode(int value) => Value = value;
    }

    public class RealNode : ASTNode
    {
        public double Value { get; }
        public RealNode(double value) => Value = value;
    }

    public class BooleanNode : ASTNode
    {
        public bool Value { get; }
        public BooleanNode(bool value) => Value = value;
    }

    public class IdentifierNode : ASTNode
    {
        public string Name { get; }
        public IdentifierNode(string name) => Name = name;
    }

    public class StringNode : ASTNode
    {
        public string Value { get; }
        public StringNode(string value) => Value = value;
    }

    public class ListNode : ASTNode
    {
        public List<ASTNode> Elements { get; }
        public ListNode(List<ASTNode> elements) => Elements = elements;
    }

    public class FunctionCallNode : ASTNode
    {
        public string FunctionName { get; }
        public List<ASTNode> Arguments { get; }
        public FunctionCallNode(string functionName, List<ASTNode> arguments)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }
    }

    public class QuoteNode : ASTNode
    {
        public ASTNode Expression { get; }
        public QuoteNode(ASTNode expression) => Expression = expression;
    }

    public class SetqNode : ASTNode
    {
        public string VariableName { get; }
        public ASTNode Value { get; }
        public SetqNode(string variableName, ASTNode value)
        {
            VariableName = variableName;
            Value = value;
        }
    }

    public class ConditionNode : ASTNode
    {
        public ASTNode Test { get; }
        public ASTNode ResultTrue { get; }
        public ASTNode ResultFalse { get; }
        public ConditionNode(ASTNode test, ASTNode resultTrue, ASTNode resultFalse=null)
        {
            Test = test;
            ResultTrue = resultTrue;
            ResultFalse = resultFalse;
        }
    }

    public class FuncNode : ASTNode
    {
        public string FunctionName { get; }
        public List<string> Parameters { get; }
        public List<ASTNode> Expressions { get; }
        public FuncNode(string functionName, List<string> parameters, List<ASTNode> expressions)
        {
            FunctionName = functionName;
            Parameters = parameters;
            Expressions = expressions;
        }
    }

    public class LambdaNode : ASTNode
    {
        public List<string> Parameters { get; }
        public ASTNode Body { get; }
        public LambdaNode(List<string> parameters, ASTNode body)
        {
            Parameters = parameters;
            Body = body;
        }
    }

    public class LambdaCallNode : ASTNode
    {
        public LambdaNode Lambda { get; }
        public List<ASTNode> Arguments { get; }
        public LambdaCallNode(LambdaNode lambda, List<ASTNode> arguments)
        {
            Lambda = lambda;
            Arguments = arguments;
        }
    }

    public class ProgNode : ASTNode
    {
        public List<string> Parameters { get; }
        public List<ASTNode> Expressions { get; }
        public ProgNode(List<string> parameters, List<ASTNode> expressions)
        {
            Parameters = parameters;
            Expressions = expressions;
        }
    }

    public class WhileNode : ASTNode
    {
        public ASTNode Condition { get; }
        public List<ASTNode> Expressions { get; }
        public WhileNode(ASTNode condition, List<ASTNode> expressions)
        {
            Condition = condition;
            Expressions = expressions;
        }
    }
}