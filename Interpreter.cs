using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace F_Interpretator
{
    public class Interpreter
    {

        private Stack<Dictionary<string, object>> scopes = new Stack<Dictionary<string, object>>();

        public Interpreter()
        {
            scopes.Push(new Dictionary<string, object>());
        }

        public void Interpret(ProgramNode program)
        {
            try
            {
                foreach (var expression in program.Expressions)
                {
                    var result = Evaluate(expression);
                    if (result != null && !(result is FuncNode))
                    {
                        Console.WriteLine($"-> {FormatResult(result)}");
                    }
                    else if (result is FuncNode)
                    {
                        scopes.Peek()[((FuncNode)result).FunctionName] = result;
                    }
                }
            }
            catch (RuntimeException ex)
            {
                Console.WriteLine($"RUNTIME ERROR: {ex.Message}");
            }
            catch (BreakException)
            {
                Console.WriteLine("-> break");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INTERPRETER ERROR: {ex.Message}");
            }
        }

        private object Evaluate(ASTNode node)
        {
            try
            {
                switch (node)
                {
                    case ProgramNode program: return EvaluateProgram(program);
                    case IntegerNode intNode: return intNode.Value;
                    case RealNode realNode: return realNode.Value;
                    case BooleanNode boolNode: return boolNode.Value;
                    case NullNode: return "null";
                    case IdentifierNode idNode: return EvaluateIdentifier(idNode);
                    case ListNode listNode: return EvaluateList(listNode);
                    case FunctionCallNode funcCall: return EvaluateFunctionCall(funcCall);
                    case SetqNode setqNode: return EvaluateSetq(setqNode);
                    case FuncNode funcNode: return EvaluateFunc(funcNode);
                    case ProgNode progNode: return EvaluateProg(progNode);
                    case WhileNode whileNode: return EvaluateWhile(whileNode);
                    case LambdaNode lambdaNode: return lambdaNode;
                    case LambdaCallNode lambdaCall: return EvaluateLambdaCall(lambdaCall);
                    case ReturnNode returnNode: return EvaluateReturn(returnNode);
                    case BreakNode: throw new BreakException();
                    case QuoteNode quoteNode: return EvaluateQuote(quoteNode);
                    default: throw new RuntimeException($"Unknown node type: {node.GetType().Name}");
                }
            }
            catch (BreakException)
            {
                throw;
            }
            catch (ReturnException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RuntimeException(ex.Message);
            }
        }

        private object EvaluateQuote(QuoteNode quoteNode)
        {
            return ConvertASTToData(quoteNode.Expression);
        }

        private object ConvertASTToData(ASTNode node)
        {
            return node switch
            {
                IntegerNode intNode => intNode.Value,
                RealNode realNode => realNode.Value,
                BooleanNode boolNode => boolNode.Value,
                NullNode => null,
                IdentifierNode idNode => idNode.Name,
                ListNode listNode => listNode.Elements.Select(ConvertASTToData).ToList(),
                _ => node.ToString()
            };
        }

        private object EvaluateProgram(ProgramNode program)
        {
            object lastResult = null;
            foreach (var expr in program.Expressions)
            {
                lastResult = Evaluate(expr);
            }
            return lastResult;
        }

        private object EvaluateIdentifier(IdentifierNode idNode)
        {
            var name = idNode.Name;

            // Проверяем специальные ключевые слова
            if (name.ToLower() == "break")
                return BreakNode.Instance;

            if (name.ToLower() == "true")
                return true;

            if (name.ToLower() == "false")
                return false;

            if (name.ToLower() == "null")
                return null;

            // Если это встроенная функция, возвращаем её имя
            if (IsBuiltInFunction(name))
                return name;

            // Ищем переменную/функцию в областях видимости
            foreach (var scope in scopes)
            {
                if (scope.ContainsKey(name))
                    return scope[name];
            }

            throw new RuntimeException($"Undefined variable: {name}");
        }

        private object EvaluateList(ListNode listNode)
        {
            if (listNode.Elements.Count == 0)
                return new List<object>();

            var firstElement = listNode.Elements[0];

            // Обработка специальных форм
            if (firstElement is IdentifierNode idNode)
            {
                var functionName = idNode.Name.ToLower();

                switch (functionName)
                {
                    case "cond": return EvaluateCond(listNode);
                    case "setq": return EvaluateSetqList(listNode);
                    case "func": return EvaluateFuncList(listNode);
                    case "prog": return EvaluateProgList(listNode);
                    case "while": return EvaluateWhileList(listNode);
                    case "lambda": return EvaluateLambdaList(listNode);
                    case "return": return EvaluateReturnList(listNode);
                    case "break": throw new BreakException();
                    case "eval": return EvaluateEval(listNode);
                }
            }

            // Обработка (quote <list>)
            if (firstElement is QuoteNode quoteNode)
            {
                return ConvertASTToData(quoteNode.Expression);
            }

            // Сначала собираем все элементы данного списка 
            var evaluatedElements = new List<object>();
            foreach (var element in listNode.Elements)
            {
                evaluatedElements.Add(Evaluate(element));
            }

            // Если первый элемент - user-defined функция, вызываем ее
            if (evaluatedElements[0] is FuncNode funcNode)
            {
                var arguments = evaluatedElements.Skip(1).ToList();
                return CallFunction(funcNode.FunctionName, arguments);
            }

            // Если первый элемент - лямбда, вызываем лямбду
            if (evaluatedElements[0] is LambdaNode lambda)
            {
                var arguments = evaluatedElements.Skip(1).ToList();
                return EvaluateLambdaCallDirect(lambda, arguments);
            }

            // Если это встроенная функция, выполняем ее
            if (evaluatedElements[0] is string builtInFuncName)
            {
                if (IsBuiltInFunction(builtInFuncName))
                {
                    var arguments = evaluatedElements.Skip(1).ToList();
                    return CallFunction(builtInFuncName, arguments);
                }
            }

            // Если первый элемент - переменная, содержащая функцию
            if (firstElement is IdentifierNode varName)
            {
                // Ищем переменную в областях видимости
                foreach (var scope in scopes)
                {
                    if (scope.ContainsKey(varName.Name))
                    {
                        var funcValue = scope[varName.Name];

                        if (funcValue is FuncNode funcFromVar)
                        {
                            var arguments = evaluatedElements.Skip(1).ToList();
                            return CallFunction(funcFromVar.FunctionName, arguments);
                        }
                        else if (funcValue is LambdaNode lambdaFromVar)
                        {
                            var arguments = evaluatedElements.Skip(1).ToList();
                            return EvaluateLambdaCallDirect(lambdaFromVar, arguments);
                        }
                        else if (funcValue is string builtInFunc && IsBuiltInFunction(builtInFunc))
                        {
                            var arguments = evaluatedElements.Skip(1).ToList();
                            return CallFunction(builtInFunc, arguments);
                        }
                    }
                }
            }

            // Возвращаем полученный список, если с ним ничего не делали
            return evaluatedElements;
        }

        private bool IsFunctionDefined(string functionName)
        {
            // Ищем в текущих областях видимости
            foreach (var scope in scopes)
            {
                if (scope.ContainsKey(functionName) && scope[functionName] is FuncNode)
                    return true;
            }
            return false;
        }

        private object EvaluateEval(ListNode evalList)
        {
            if (evalList.Elements.Count != 2)
                throw new RuntimeException("eval requires exactly 1 argument");

            var arg = Evaluate(evalList.Elements[1]);

            // Если аргумент - список, интерпретируем его
            if (arg is List<object> list)
            {
                // Преобразуем список данных обратно в AST
                var astNode = ConvertDataToAST(list);
                return Evaluate(astNode);
            }

            return arg;
        }

        private ASTNode ConvertDataToAST(object data)
        {
            return data switch
            {
                int i => new IntegerNode(i),
                double d => new RealNode(d),
                bool b => new BooleanNode(b),
                null => NullNode.Instance,
                string s => new IdentifierNode(s),
                List<object> list => new ListNode(list.Select(ConvertDataToAST).ToList()),
                _ => throw new RuntimeException($"Cannot convert to AST: {data}")
            };
        }

        private object EvaluateUserFunction(FuncNode funcNode, List<object> arguments)
        {
            if (funcNode.Parameters.Count != arguments.Count)
                throw new RuntimeException($"Function {funcNode.FunctionName} expects {funcNode.Parameters.Count} arguments, got {arguments.Count}");

            // Создаем inner-scope для функции
            var newScope = new Dictionary<string, object>();
            for (int i = 0; i < funcNode.Parameters.Count; i++)
            {
                // Сохраняем аргумент в области видимости под именем параметра
                newScope[funcNode.Parameters[i]] = arguments[i];
            }

            scopes.Push(newScope);

            object result = null;
            try
            {
                foreach (var expr in funcNode.Expressions)
                {
                    result = Evaluate(expr);
                }
            }
            catch (ReturnException ret)
            {
                result = ret.Value;
            }
            finally
            {
                scopes.Pop();
            }

            return result;
        }

        private object CallFunction(string functionName, List<object> arguments)
        {
            if (IsBuiltInFunction(functionName))
            {
                return EvaluateBuiltInFunction(functionName, arguments);
            }

            // Ищем функцию в областях видимости
            foreach (var scope in scopes)
            {
                if (scope.ContainsKey(functionName))
                {
                    var func = scope[functionName];
                    if (func is FuncNode funcNode)
                    {
                        return EvaluateUserFunction(funcNode, arguments);
                    }
                    else if (func is string builtInFunc && IsBuiltInFunction(builtInFunc))
                    {
                        return EvaluateBuiltInFunction(builtInFunc, arguments);
                    }
                }
            }

            throw new RuntimeException($"Unknown function: {functionName}");
        }

        private object EvaluateCond(ListNode condList)
        {
            if (condList.Elements[1] is ListNode condition && condList.Elements.Count > 2)
            {
                // Форма: (cond test result1 result2)
                var testResult = Evaluate(condition);
                if (ConvertToBoolean(testResult))
                {
                    return Evaluate(condList.Elements[2]);
                }
                else if (condList.Elements.Count == 4)
                {
                    return Evaluate(condList.Elements[3]);
                }
            }
            else if (condList.Elements[1] is IdentifierNode someCond && condList.Elements.Count > 2)
            {
                // Форма: (cond test result1 result2)
                if (ConvertToBoolean(Evaluate(someCond)))
                {
                    return Evaluate(condList.Elements[2]);
                }
                else if (condList.Elements.Count == 4)
                {
                    return Evaluate(condList.Elements[3]);
                }
            }
            return null;
        }

        private object EvaluateSetqList(ListNode setqList)
        {
            if (setqList.Elements.Count != 3)
                throw new RuntimeException("setq requires exactly 2 arguments: variable and value");

            if (setqList.Elements[1] is IdentifierNode varName)
            {
                var value = Evaluate(setqList.Elements[2]);
                scopes.Peek()[varName.Name] = value;
                return null;
            }
            throw new RuntimeException("setq: first argument must be a variable name");
        }

        private object EvaluateFuncList(ListNode funcList)
        {
            if (funcList.Elements.Count < 4)
                throw new RuntimeException("func requires name, parameter list and body");

            if (funcList.Elements[1] is IdentifierNode funcName &&
                funcList.Elements[2] is ListNode paramList)
            {
                var parameters = new List<string>();
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                        parameters.Add(paramId.Name);
                    else
                        throw new RuntimeException("func: parameters must be identifiers");
                }

                var bodyExpressions = funcList.Elements.Skip(3).ToList();
                return new FuncNode(funcName.Name, parameters, bodyExpressions);
            }
            throw new RuntimeException("func: invalid syntax");
        }

        private object EvaluateProgList(ListNode progList)
        {
            if (progList.Elements.Count < 3)
                throw new RuntimeException("prog requires variable list and body");

            var parameters = new List<string>();
            if (progList.Elements[1] is ListNode varList)
            {
                foreach (var param in varList.Elements)
                {
                    if (param is IdentifierNode paramId)
                        parameters.Add(paramId.Name);
                }
            }

            var bodyExpressions = progList.Elements.Skip(2).ToList();
            var progNode = new ProgNode(parameters, bodyExpressions);
            return EvaluateProg(progNode);
        }

        private object EvaluateWhileList(ListNode whileList)
        {
            if (whileList.Elements.Count < 3)
                throw new RuntimeException("while requires condition and body");

            var condition = whileList.Elements[1];
            var bodyExpressions = whileList.Elements.Skip(2).ToList();
            var whileNode = new WhileNode(condition, bodyExpressions);
            return EvaluateWhile(whileNode);
        }

        private object EvaluateLambdaList(ListNode lambdaList)
        {
            if (lambdaList.Elements.Count != 3)
                throw new RuntimeException("lambda requires parameter list and body");

            var parameters = new List<string>();
            if (lambdaList.Elements[1] is ListNode paramList)
            {
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                        parameters.Add(paramId.Name);
                }
            }

            var body = lambdaList.Elements[2];
            return new LambdaNode(parameters, body);
        }

        private object EvaluateReturnList(ListNode returnList)
        {
            object value = null;
            if (returnList.Elements.Count == 2)
            {
                value = Evaluate(returnList.Elements[1]);
            }
            throw new ReturnException(value);
        }

        private object EvaluateFunctionCall(FunctionCallNode funcCall)
        {
            var functionName = funcCall.FunctionName;
            var arguments = funcCall.Arguments.Select(Evaluate).ToList();
            return CallFunction(functionName, arguments);
        }

        private object EvaluateLambdaCallDirect(LambdaNode lambda, List<object> arguments)
        {
            if (lambda.Parameters.Count > arguments.Count)
                throw new RuntimeException($"Lambda expects at least {lambda.Parameters.Count} arguments, got {arguments.Count}");

            // Создаем область видимости для параметров лямбды
            var newScope = new Dictionary<string, object>();
            for (int i = 0; i < lambda.Parameters.Count; i++)
            {
                newScope[lambda.Parameters[i]] = arguments[i];
            }

            scopes.Push(newScope);
            var result = Evaluate(lambda.Body);
            scopes.Pop();

            // ЕСЛИ РЕЗУЛЬТАТ - ИМЯ ФУНКЦИИ И ЕСТЬ ДОПОЛНИТЕЛЬНЫЕ АРГУМЕНТЫ
            if (result is string funcName && arguments.Count > lambda.Parameters.Count)
            {
                var remainingArgs = arguments.Skip(lambda.Parameters.Count).ToList();
                return CallFunction(funcName, remainingArgs);
            }

            return result;
        }

        private object EvaluateBuiltInFunction(string functionName, List<object> arguments)
        {
            return functionName.ToLower() switch
            {
                "plus" => EvaluatePlus(arguments),
                "minus" => EvaluateMinus(arguments),
                "times" => EvaluateTimes(arguments),
                "divide" => EvaluateDivide(arguments),
                "less" => EvaluateLess(arguments),
                "lesseq" => EvaluateLessEq(arguments),
                "greater" => EvaluateGreater(arguments),
                "greatereq" => EvaluateGreaterEq(arguments),
                "equal" => EvaluateEqual(arguments),
                "nonequal" => EvaluateNotEqual(arguments),
                "and" => EvaluateAnd(arguments),
                "or" => EvaluateOr(arguments),
                "not" => EvaluateNot(arguments),
                "head" => EvaluateHead(arguments),
                "tail" => EvaluateTail(arguments),
                "cons" => EvaluateCons(arguments),
                "isint" => EvaluateIsInt(arguments),
                "isreal" => EvaluateIsReal(arguments),
                "isbool" => EvaluateIsBool(arguments),
                "isnull" => EvaluateIsNull(arguments),
                "isatom" => EvaluateIsAtom(arguments),
                "islist" => EvaluateIsList(arguments),
                _ => throw new RuntimeException($"Unknown built-in function: {functionName}")
            };
        }

        private object EvaluatePlus(List<object> args)
        {
            ValidateArgumentsCount("plus", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);

            if (left is int leftInt && right is int rightInt)
                return leftInt + rightInt;
            else
                return Convert.ToDouble(left) + Convert.ToDouble(right);
        }

        private object EvaluateMinus(List<object> args)
        {
            ValidateArgumentsCount("minus", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);

            if (left is int leftInt && right is int rightInt)
                return leftInt - rightInt;
            else
                return Convert.ToDouble(left) - Convert.ToDouble(right);
        }

        private object EvaluateTimes(List<object> args)
        {
            ValidateArgumentsCount("times", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);

            if (left is int leftInt && right is int rightInt)
                return leftInt * rightInt;
            else
                return Convert.ToDouble(left) * Convert.ToDouble(right);
        }

        private object EvaluateDivide(List<object> args)
        {
            ValidateArgumentsCount("divide", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);

            // Деление всегда возвращает double
            var leftVal = left is int leftInt ? (double)leftInt : (double)left;
            var rightVal = right is int rightInt ? (double)rightInt : (double)right;

            if (Math.Abs(rightVal) < 0.000001) throw new RuntimeException("Division by zero");
            return leftVal / rightVal;
        }

        private object EvaluateLess(List<object> args)
        {
            ValidateArgumentsCount("less", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);

            if (left is int leftInt && right is int rightInt)
                return leftInt < rightInt;
            else
                return Convert.ToDouble(left) < Convert.ToDouble(right);
        }

        private object EvaluateLessEq(List<object> args)
        {
            ValidateArgumentsCount("lesseq", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);
            if (left is int leftInt && right is int rightInt)
                return leftInt <= rightInt;
            else
                return Convert.ToDouble(left) <= Convert.ToDouble(right);
        }

        private object EvaluateGreater(List<object> args)
        {
            ValidateArgumentsCount("greater", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);
            if (left is int leftInt && right is int rightInt)
                return leftInt > rightInt;
            else
                return Convert.ToDouble(left) > Convert.ToDouble(right);
        }

        private object EvaluateGreaterEq(List<object> args)
        {
            ValidateArgumentsCount("greatereq", 2, args.Count);
            var left = ConvertToNumber(args[0]);
            var right = ConvertToNumber(args[1]);
            if (left is int leftInt && right is int rightInt)
                return leftInt >= rightInt;
            else
                return Convert.ToDouble(left) >= Convert.ToDouble(right);
        }

        private object EvaluateEqual(List<object> args)
        {
            ValidateArgumentsCount("equal", 2, args.Count);
            return AreEqual(args[0], args[1]);
        }

        private object EvaluateNotEqual(List<object> args)
        {
            ValidateArgumentsCount("nonequal", 2, args.Count);
            return !AreEqual(args[0], args[1]);
        }

        private object EvaluateAnd(List<object> args)
        {
            ValidateArgumentsCount("and", 2, args.Count);
            return ConvertToBoolean(args[0]) && ConvertToBoolean(args[1]);
        }

        private object EvaluateOr(List<object> args)
        {
            ValidateArgumentsCount("or", 2, args.Count);
            return ConvertToBoolean(args[0]) || ConvertToBoolean(args[1]);
        }

        private object EvaluateNot(List<object> args)
        {
            ValidateArgumentsCount("not", 1, args.Count);
            return !ConvertToBoolean(args[0]);
        }

        private object EvaluateHead(List<object> args)
        {
            ValidateArgumentsCount("head", 1, args.Count);
            if (args[0] is List<object> list && list.Count > 0)
                return list[0];
            throw new RuntimeException("head: argument is not a list or list is empty");
        }

        private object EvaluateTail(List<object> args)
        {
            ValidateArgumentsCount("tail", 1, args.Count);
            if (args[0] is List<object> list && list.Count > 0)
                return list.Skip(1).ToList();
            throw new RuntimeException("tail: argument is not a list or list is empty");
        }

        private object EvaluateCons(List<object> args)
        {
            ValidateArgumentsCount("cons", 2, args.Count);
            if (args[1] is List<object> list)
            {
                var newList = new List<object> { args[0] };
                newList.AddRange(list);
                return newList;
            }
            throw new RuntimeException("cons: second argument is not a list");
        }

        private object EvaluateIsInt(List<object> args)
        {
            ValidateArgumentsCount("isint", 1, args.Count);
            return args[0] is IntegerNode || args[0] is int;
        }

        private object EvaluateIsReal(List<object> args)
        {
            ValidateArgumentsCount("isreal", 1, args.Count);
            return args[0] is RealNode || args[0] is double;
        }

        private object EvaluateIsBool(List<object> args)
        {
            ValidateArgumentsCount("isbool", 1, args.Count);
            return args[0] is BooleanNode || args[0] is bool;
        }

        private object EvaluateIsNull(List<object> args)
        {
            ValidateArgumentsCount("isnull", 1, args.Count);
            return args[0] is null || args[0] is NullNode;
        }

        private object EvaluateIsAtom(List<object> args)
        {
            ValidateArgumentsCount("isatom", 1, args.Count);
            return IsAtom(args[0]);
        }

        private object EvaluateIsList(List<object> args)
        {
            ValidateArgumentsCount("islist", 1, args.Count);
            return args[0] is List<object>;
        }

        private object EvaluateSetq(SetqNode setqNode)
        {
            var value = Evaluate(setqNode.Value);
            scopes.Peek()[setqNode.VariableName] = value;
            return null;
        }

        private object EvaluateFunc(FuncNode funcNode)
        {
            scopes.Peek()[funcNode.FunctionName] = funcNode;
            return funcNode;
        }

        private object EvaluateProg(ProgNode progNode)
        {
            var newScope = new Dictionary<string, object>();
            foreach (var param in progNode.Parameters)
            {
                newScope[param] = null;
            }

            scopes.Push(newScope);

            object result = null;
            try
            {
                foreach (var expr in progNode.Expressions)
                {
                    result = Evaluate(expr);
                    if (result != null && !(result is FuncNode))
                    {
                        Console.WriteLine($"-> {FormatResult(result)}");
                    }
                    else if (result is FuncNode funcNode1)
                    {
                        scopes.Peek()[funcNode1.FunctionName] = funcNode1;
                    }
                }
            }
            finally
            {
                scopes.Pop();
            }

            return result;
        }

        private object EvaluateWhile(WhileNode whileNode)
        {
            object result = null;
            while (true)
            {
                var condition = Evaluate(whileNode.Condition);
                if (!ConvertToBoolean(condition))
                    break;

                try
                {
                    foreach (var expr in whileNode.Expressions)
                    {
                        result = Evaluate(expr);
                        if (result != null && !(result is FuncNode))
                        {
                            Console.WriteLine($"-> {FormatResult(result)}");
                        }
                        else if (result is FuncNode)
                        {
                            scopes.Peek()[((FuncNode)result).FunctionName] = (FuncNode)result;
                        }
                    }
                }
                catch (BreakException)
                {
                    break;
                }
            }

            return result;
        }

        private object EvaluateLambdaCall(LambdaCallNode lambdaCall)
        {
            var lambda = lambdaCall.Lambda;
            var arguments = lambdaCall.Arguments.Select(Evaluate).ToList();

            if (lambda.Parameters.Count > arguments.Count)
                throw new RuntimeException($"Lambda expects at least {lambda.Parameters.Count} arguments, got {arguments.Count}");

            var newScope = new Dictionary<string, object>();
            for (int i = 0; i < lambda.Parameters.Count; i++)
            {
                newScope[lambda.Parameters[i]] = arguments[i];
            }

            scopes.Push(newScope);
            var result = Evaluate(lambda.Body);
            scopes.Pop();

            // Если лямбда вернула имя функции и есть дополнительные аргументы, вызываем её
            if (result is string returnedFuncName && arguments.Count > lambda.Parameters.Count)
            {
                var remainingArgs = arguments.Skip(lambda.Parameters.Count).ToList();

                if (IsBuiltInFunction(returnedFuncName))
                {
                    return EvaluateBuiltInFunction(returnedFuncName, remainingArgs);
                }
                else 
                {
                    foreach (var scope in scopes)
                    {
                        if (scope.ContainsKey(returnedFuncName))
                            return EvaluateUserFunction((FuncNode)scope[returnedFuncName], remainingArgs);
                    }
                }
            }

            return result;
        }

        private object EvaluateReturn(ReturnNode returnNode)
        {
            var value = Evaluate(returnNode.Value);
            throw new ReturnException(value);
        }

        private object ConvertToNumber(object value)
        {
            if (value is int)
            {
                return (int)value;
            }
            else if (value is double)
            {
                return (double)value;
            }
            else
            {
                throw new RuntimeException($"Expected number, got {value?.GetType().Name}");
            }
        }

        private bool ConvertToBoolean(object value)
        {
            return value switch
            {
                bool b => b,
                int i => i != 0,
                double d => Math.Abs(d) > 0.000001,
                string s => !string.IsNullOrEmpty(s),
                List<object> list => list.Count > 0,
                null => false,
                _ => true
            };
        }

        private bool AreEqual(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;
            if (left.GetType() != right.GetType()) return false;

            return left.Equals(right);
        }

        private bool IsAtom(object value)
        {
            return value is IntegerNode || value is RealNode || value is BooleanNode || value is NullNode 
                        || value is int || value is double || value is bool || value is null;
        }

        private bool IsBuiltInFunction(string name)
        {
            return name.ToLower() switch
            {
                "plus" or "minus" or "times" or "divide" or
                "less" or "lesseq" or "greater" or "greatereq" or "equal" or "nonequal" or
                "and" or "or" or "not" or
                "head" or "tail" or "cons" or
                "isint" or "isreal" or "isbool" or "isnull" or "isatom" or "islist" or
                "print" or "eval" => true,
                _ => false
            };
        }

        private void ValidateArgumentsCount(string functionName, int expected, int actual)
        {
            if (actual != expected)
                throw new RuntimeException($"Function {functionName} expects {expected} arguments, got {actual}");
        }

        private string FormatResult(object result)
        {
            return result switch
            {
                null => "null",
                bool b => b.ToString().ToLower(),
                double d => d.ToString(CultureInfo.InvariantCulture),
                List<object> list => FormatList(list),
                FuncNode func => $"<function {func.FunctionName}>",
                LambdaNode lambda => "<lambda>",
                _ => result.ToString()
            };
        }

        private string FormatList(List<object> list)
        {
            // Если список содержит другие списки, форматируем их рекурсивно
            var elements = list.Select(item =>
            {
                if (item is List<object> innerList)
                {
                    return FormatList(innerList);
                }
                return FormatResult(item);
            });
            return $"({string.Join(" ", elements)})";
        }
    }

    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message) { }
    }

    public class ReturnException : Exception
    {
        public object Value { get; }
        public ReturnException(object value) : base("Return from function")
        {
            Value = value;
        }
    }

    public class BreakException : Exception
    {
        public BreakException() : base("Break from loop") { }
    }
}