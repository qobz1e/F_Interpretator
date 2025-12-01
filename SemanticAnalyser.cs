using System;
using System.Collections.Generic;
using System.Linq;

namespace F_Interpretator
{
    public class SemanticAnalyzer
    {
        private List<string> errors = new List<string>();
        private List<string> warnings = new List<string>();
        private Dictionary<string, int> declaredFunctions = new Dictionary<string, int>();
        private Stack<HashSet<string>> variableScopes = new Stack<HashSet<string>>();
        private HashSet<string> usedFunctions = new HashSet<string>();
        private HashSet<string> declaredVars = new HashSet<string>();
        private HashSet<string> usedVars = new HashSet<string>();
        private int loopDepth = 0;
        private int functionDepth = 0;

        public bool HasErrors => errors.Count > 0;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        public ProgramNode Analyze(ProgramNode program)
        {
            //Console.WriteLine("=== Semantic Analysis Started ===");
            variableScopes.Push(new HashSet<string>());

            // Phase 1: Сбор функций
            //CollectFunctions(program);

            // Phase 2: Проверки
            PerformChecks(program);

            // Phase 3: Оптимизации (только если нет ошибок)
            var optimizedProgram = program;
            //if (!HasErrors)
            //{
            //    optimizedProgram = ApplyOptimizations(program);
            //}

            //PrintResults();
            return optimizedProgram;
        }

        #region Phase 1: Сбор Функций

        private void CollectFunctions(ProgramNode program)
        {
            foreach (var expr in program.Expressions)
            {
                if (expr is ListNode list && list.Elements.Count > 0)
                {
                    if (list.Elements[0] is IdentifierNode id && id.Name.ToLower() == "func")
                    {
                        // (func name (params) body...)
                        if (list.Elements.Count >= 3 &&
                            list.Elements[1] is IdentifierNode funcName &&
                            list.Elements[2] is ListNode paramList)
                        {
                            var paramCount = paramList.Elements.Count;
                            if (declaredFunctions.ContainsKey(funcName.Name))
                            {
                                errors.Add($"Function '{funcName.Name}' is already declared");
                            }
                            else
                            {
                                declaredFunctions[funcName.Name] = paramCount;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Phase 2: Проверки

        private void PerformChecks(ProgramNode program)
        {
            foreach (var expr in program.Expressions)
            {
                CheckNode(expr);
            }

            // CHECK 4: Неиспользуемые переменные
            foreach (var declaredVar in declaredVars)
            {
                if (!usedVars.Contains(declaredVar))
                {
                    warnings.Add($"Variable '{declaredVar}' is declared but never used");
                }
            }
        }

        private void CheckNode(ASTNode node)
        {
            if (node is ListNode list && list.Elements.Count > 0)
            {
                if (list.Elements[0] is IdentifierNode id)
                {
                    string keyword = id.Name.ToLower();

                    // Обработка специальных форм
                    switch (keyword)
                    {
                        case "func":
                            CheckFunc(list);
                            return;
                        case "setq":
                            CheckSetq(list);
                            return;
                        case "cond":
                            CheckCond(list);
                            return;
                        case "while":
                            CheckWhile(list);
                            return;
                        case "prog":
                            CheckProg(list);
                            return;
                        case "lambda":
                            CheckLambda(list);
                            return;
                        case "return":
                            CheckReturn(list);
                            return;
                        case "break":
                            // CHECK 2: break только в циклах
                            if (loopDepth == 0)
                                errors.Add("'break' can only be used inside a loop");
                            return;
                        case "quote":
                            // Quote не оценивается
                            return;
                        default:
                            // Это вызов функции
                            CheckFunctionCall(list);
                            return;
                    }
                }
            }

            // Рекурсивная проверка для других узлов
            if (node is IdentifierNode identifier)
            {
                usedVars.Add(identifier.Name);
            }
        }

        private void CheckFunc(ListNode list)
        {
            // (func name (params) body...)

            if (list.Elements[1] is IdentifierNode funcName &&
    list.Elements[2] is ListNode paramList1)
            {
                if (declaredFunctions.ContainsKey(funcName.Name))
                {
                    errors.Add($"Function '{funcName.Name}' is already declared");
                }
                else
                {
                    declaredFunctions[funcName.Name] = paramList1.Elements.Count;
                }
            }

            if (list.Elements.Count < 4)
            {
                errors.Add("func requires at least: name, parameter list, and body");
                return;
            }

            if (list.Elements[2] is ListNode paramList)
            {
                functionDepth++;
                var localVars = new HashSet<string>();

                // Добавляем параметры как локальные переменные
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                    {
                        localVars.Add(paramId.Name);
                        declaredVars.Add(paramId.Name);
                    }
                }

                variableScopes.Push(localVars);

                // Проверяем тело функции
                for (int i = 3; i < list.Elements.Count; i++)
                {
                    CheckNode(list.Elements[i]);
                }

                variableScopes.Pop();
                functionDepth--;
            }
        }

        private void CheckSetq(ListNode list)
        {
            // (setq var value)
            if (list.Elements.Count != 3)
            {
                errors.Add("setq requires exactly 2 arguments: variable and value");
                return;
            }

            if (list.Elements[1] is IdentifierNode varName)
            {
                declaredVars.Add(varName.Name);
                // usedVars.Add(varName.Name); // Переменная используется при присваивании

                // Добавляем в текущую область видимости
                if (variableScopes.Count > 0)
                    variableScopes.Peek().Add(varName.Name);
            }

            // Проверяем значение
            CheckNode(list.Elements[2]);
        }

        private bool IsVariableDeclared(string name)
        {
            // Проверяем по всему стеку областей видимости
            foreach (var scope in variableScopes)
            {
                if (scope.Contains(name))
                    return true;
            }
            return false;
        }

        private void CheckCond(ListNode list)
        {
            // (cond test then [else])
            if (list.Elements.Count < 3)
            {
                errors.Add("cond requires at least test and then branch");
                return;
            }

            // Проверяем все части
            for (int i = 1; i < list.Elements.Count; i++)
            {
                CheckNode(list.Elements[i]);
            }
        }

        private void CheckWhile(ListNode list)
        {
            // (while condition body...)
            if (list.Elements.Count < 3)
            {
                errors.Add("while requires condition and at least one body expression");
                return;
            }

            loopDepth++;

            // Проверяем условие
            CheckNode(list.Elements[1]);

            // Проверяем тело
            for (int i = 2; i < list.Elements.Count; i++)
            {
                CheckNode(list.Elements[i]);
            }

            loopDepth--;
        }

        private void CheckProg(ListNode list)
        {
            // (prog (vars) body...)
            if (list.Elements.Count < 3)
            {
                errors.Add("prog requires variable list and body");
                return;
            }

            functionDepth++;
            var localVars = new HashSet<string>();

            if (list.Elements[1] is ListNode varList)
            {
                foreach (var v in varList.Elements)
                {
                    if (v is IdentifierNode varId)
                    {
                        localVars.Add(varId.Name);
                        declaredVars.Add(varId.Name);
                    }
                }
            }

            variableScopes.Push(localVars);

            for (int i = 2; i < list.Elements.Count; i++)
            {
                CheckNode(list.Elements[i]);
            }

            variableScopes.Pop();
            functionDepth--;
        }

        private void CheckLambda(ListNode list)
        {
            // (lambda (params) body)
            if (list.Elements.Count < 3)
            {
                errors.Add("lambda requires parameter list and body");
                return;
            }

            functionDepth++;
            var localVars = new HashSet<string>();

            if (list.Elements[1] is ListNode paramList)
            {
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                    {
                        localVars.Add(paramId.Name);
                        declaredVars.Add(paramId.Name);
                    }
                }
            }

            variableScopes.Push(localVars);
            CheckNode(list.Elements[2]);
            variableScopes.Pop();
            functionDepth--;
        }

        private void CheckReturn(ListNode list)
        {
            // CHECK 2: return только в функциях
            if (functionDepth == 0)
                errors.Add("'return' can only be used inside a function");

            if (list.Elements.Count == 2)
            {
                CheckNode(list.Elements[1]);
            }
        }

        private void CheckFunctionCall(ListNode list)
        {
            if (list.Elements[0] is IdentifierNode funcId)
            {
                string funcName = funcId.Name;

                // CHECK 1: Проверка объявления функции
                if (!IsBuiltInFunction(funcName))
                {
                    // Проверяем, может это переменная с lambda?
                    if (IsVariableDeclared(funcName))
                    {
                        usedVars.Add(funcName); // Lambda используется
                    }
                    else if (declaredFunctions.ContainsKey(funcName))
                    {
                        usedFunctions.Add(funcName);

                        // CHECK 3: Проверка количества параметров
                        int expectedParams = declaredFunctions[funcName];
                        int actualParams = list.Elements.Count - 1;

                        if (expectedParams != actualParams)
                        {
                            errors.Add($"Function '{funcName}' expects {expectedParams} parameter(s) but got {actualParams}");
                        }
                    }
                    else
                    {
                        errors.Add($"Function '{funcName}' is not declared");
                    }
                }

                // Проверяем аргументы
                for (int i = 1; i < list.Elements.Count; i++)
                {
                    CheckNode(list.Elements[i]);
                }
            }
        }

        #endregion

        #region Phase 3: Оптимизации

        private ProgramNode ApplyOptimizations(ProgramNode program)
        {
            // OPT 1: Constant folding
            var afterConstFolding = new ProgramNode(
                program.Expressions.Select(e => FoldConstants(e)).ToList());

            // OPT 2: Condition simplification
            var afterCondSimplify = new ProgramNode(
                afterConstFolding.Expressions.Select(e => SimplifyConditions(e)).ToList());

            // OPT 3: Unreachable code removal
            var afterUnreachable = new ProgramNode(
                afterCondSimplify.Expressions.Select(e => RemoveUnreachable(e)).ToList());


            // OPT 3.5: Unused vars removal
            var afterUnusedVarsRemoval = RemoveUnusedVariables(afterUnreachable);
            // OPT 4: Unused function removal
            var afterUnusedRemoval = RemoveUnusedFunctions(afterUnusedVarsRemoval);

            return afterUnusedRemoval;
        }
        // OPT 1: Constant Expression Folding
        /*private ASTNode FoldConstants(ASTNode node)
        {
            if (node is ListNode list && list.Elements.Count > 0)
            {
                if (list.Elements[0] is IdentifierNode id)
                {
                    string op = id.Name.ToLower();

                    // Для setq - оптимизируем значение
                    if (op == "setq" && list.Elements.Count == 3)
                    {
                        return new ListNode(new List<ASTNode>
                        {
                            list.Elements[0],
                            list.Elements[1],
                            FoldConstants(list.Elements[2])
                        });
                    }

                    // Для func, while, prog, lambda, cond - оптимизируем рекурсивно
                    if (op == "func" || op == "while" || op == "prog" || op == "lambda" || op == "cond")
                    {
                        return new ListNode(list.Elements.Select(e => FoldConstants(e)).ToList());
                    }
                    *//*if (op == "setq" && list.Elements.Count == 3)
                    {
                        return new ListNode(new List<ASTNode>
                        {
                            list.Elements[0],
                            list.Elements[1],
                            FoldConstants(list.Elements[2])
                        });
                    }*//*

                    // Сначала оптимизируем аргументы
                    var optimizedElements = list.Elements.Select(e => FoldConstants(e)).ToList();

                    // Пытаемся вычислить константное выражение
                    if (IsArithmeticOp(op) || IsComparisonOp(op) || IsLogicalOp(op))
                    {
                        var args = optimizedElements.Skip(1).ToList();
                        var result = TryEvaluate(op, args);
                        if (result != null)
                            return result;
                    }

                    return new ListNode(optimizedElements);
                }
            }

            return node;
        }

        private ASTNode TryEvaluate(string op, List<ASTNode> args)
        {
            try
            {
                switch (op)
                {
                    case "plus":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double result = v1.Value + v2.Value;
                                return IsWholeNumber(result) ?
                                    (ASTNode)new IntegerNode((int)result) : new RealNode(result);
                            }
                        }
                        break;

                    case "minus":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double result = v1.Value - v2.Value;
                                return IsWholeNumber(result) ?
                                    (ASTNode)new IntegerNode((int)result) : new RealNode(result);
                            }
                        }
                        break;

                    case "times":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double result = v1.Value * v2.Value;
                                return IsWholeNumber(result) ?
                                    (ASTNode)new IntegerNode((int)result) : new RealNode(result);
                            }
                        }
                        break;

                    case "divide":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue && v2.Value != 0)
                                return new RealNode(v1.Value / v2.Value);
                        }
                        break;

                    case "less":
                    case "lesseq":
                    case "greater":
                    case "greatereq":
                    case "equal":
                    case "nonequal":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                bool result = op switch
                                {
                                    "less" => v1.Value < v2.Value,
                                    "lesseq" => v1.Value <= v2.Value,
                                    "greater" => v1.Value > v2.Value,
                                    "greatereq" => v1.Value >= v2.Value,
                                    "equal" => Math.Abs(v1.Value - v2.Value) < 0.000001,
                                    "nonequal" => Math.Abs(v1.Value - v2.Value) >= 0.000001,
                                    _ => false
                                };
                                return new BooleanNode(result);
                            }
                        }
                        break;

                    case "and":
                        if (args.Count == 2 && args[0] is BooleanNode b1 && args[1] is BooleanNode b2)
                            return new BooleanNode(b1.Value && b2.Value);
                        break;

                    case "or":
                        if (args.Count == 2 && args[0] is BooleanNode b3 && args[1] is BooleanNode b4)
                            return new BooleanNode(b3.Value || b4.Value);
                        break;

                    case "not":
                        if (args.Count == 1 && args[0] is BooleanNode b5)
                            return new BooleanNode(!b5.Value);
                        break;

                    case "isint":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is IntegerNode);
                        break;

                    case "isreal":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is RealNode);
                        break;

                    case "isbool":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is BooleanNode);
                        break;

                    case "isnull":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is NullNode);
                        break;

                    case "isatom":
                        if (args.Count == 1)
                            return new BooleanNode(
                                args[0] is IntegerNode ||
                                args[0] is RealNode ||
                                args[0] is BooleanNode ||
                                args[0] is NullNode ||
                                args[0] is IdentifierNode
                            );
                        break;

                    case "islist":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is ListNode);
                        break;

                }
            }
            catch { }

            return null;
        }
        // OPT 1: Constant Expression Folding*/
        private ASTNode FoldConstants(ASTNode node)
        {
            // Если узел — список (вызов / специальная форма)
            if (node is ListNode list && list.Elements.Count > 0)
            {
                // Если это quote — не вычисляем содержимое, оставляем как есть
                if (list.Elements[0] is IdentifierNode id0 && id0.Name.ToLower() == "quote")
                {
                    // Не сворачиваем внутри quote — сохраняем литерал
                    return list;
                }

                // Специальные формы, которые не должны быть вычислены в константу целиком.
                // Их аргументы можно сворачивать, но сам form не заменяем на значение.
                var headName = (list.Elements[0] as IdentifierNode)?.Name.ToLower();
                var specialForms = new HashSet<string> { "func", "while", "prog", "lambda", "cond", "return", "break", "quote" };
                if (headName != null && specialForms.Contains(headName))
                {
                    // Рекурсивно сворачиваем аргументы, но не пытаемся вычислить весь список
                    var elems = list.Elements.Select(e => FoldConstants(e)).Where(e => e != null).ToList();
                    return new ListNode(elems);
                }

                // Общий случай: рекурсивно сворачиваем аргументы
                var optimizedElements = list.Elements.Select(e => FoldConstants(e)).Where(e => e != null).ToList();

                // Попробовать вычислить как константу (операция и все аргументы — константы)
                var op = (optimizedElements[0] as IdentifierNode)?.Name.ToLower();
                if (op != null)
                {
                    var args = optimizedElements.Skip(1).ToList();
                    var result = TryEvaluate(op, args);
                    if (result != null)
                        return result;
                }

                // Иначе возвращаем список с уже оптимизированными элементами
                return new ListNode(optimizedElements);
            }

            // Если это quote-узел обёртка типа QuoteNode — оставить как есть (если есть такой тип)
            // В исходном коде quote представлен как ListNode с идентификатором "quote", поэтому
            // отдельной работы тут обычно не нужно.

            // Всё остальное — не-листовые узлы (числа, boolean, id и т.п.) — возвращаем как есть
            return node;
        }

        private ASTNode TryEvaluate(string op, List<ASTNode> args)
        {
            try
            {
                switch (op)
                {
                    // Arithmetic (по 2 аргументам)
                    case "plus":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double sum = v1.Value + v2.Value;
                                return IsWholeNumber(sum) ? (ASTNode)new IntegerNode((int)Math.Round(sum)) : new RealNode(sum);
                            }
                        }
                        break;

                    case "minus":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double r = v1.Value - v2.Value;
                                return IsWholeNumber(r) ? (ASTNode)new IntegerNode((int)Math.Round(r)) : new RealNode(r);
                            }
                        }
                        break;

                    case "times":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue)
                            {
                                double r = v1.Value * v2.Value;
                                return IsWholeNumber(r) ? (ASTNode)new IntegerNode((int)Math.Round(r)) : new RealNode(r);
                            }
                        }
                        break;

                    case "divide":
                        if (args.Count == 2)
                        {
                            var v1 = GetNumber(args[0]);
                            var v2 = GetNumber(args[1]);
                            if (v1.HasValue && v2.HasValue && Math.Abs(v2.Value) > 0.0000001)
                            {
                                double r = v1.Value / v2.Value;
                                return new RealNode(r);
                            }
                        }
                        break;

                    // Comparisons
                    case "less":
                    case "lesseq":
                    case "greater":
                    case "greatereq":
                    case "equal":
                    case "nonequal":
                        if (args.Count == 2)
                        {
                            var a = GetNumber(args[0]);
                            var b = GetNumber(args[1]);
                            if (a.HasValue && b.HasValue)
                            {
                                bool res = op switch
                                {
                                    "less" => a.Value < b.Value,
                                    "lesseq" => a.Value <= b.Value,
                                    "greater" => a.Value > b.Value,
                                    "greatereq" => a.Value >= b.Value,
                                    "equal" => Math.Abs(a.Value - b.Value) < 0.000001,
                                    "nonequal" => Math.Abs(a.Value - b.Value) >= 0.000001,
                                    _ => false
                                };
                                return new BooleanNode(res);
                            }
                        }
                        break;

                    // Logical
                    case "and":
                        if (args.Count == 2 && args[0] is BooleanNode b1 && args[1] is BooleanNode b2)
                            return new BooleanNode(b1.Value && b2.Value);
                        break;

                    case "or":
                        if (args.Count == 2 && args[0] is BooleanNode b3 && args[1] is BooleanNode b4)
                            return new BooleanNode(b3.Value || b4.Value);
                        break;

                    case "not":
                        if (args.Count == 1 && args[0] is BooleanNode b5)
                            return new BooleanNode(!b5.Value);
                        break;

                    // Type predicates and null/atom/list checks
                    case "isint":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is IntegerNode);
                        break;

                    case "isreal":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is RealNode);
                        break;

                    case "isbool":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is BooleanNode);
                        break;

                    case "isnull":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is NullNode);
                        break;

                    case "isatom":
                        if (args.Count == 1)
                        {
                            bool isAtom = args[0] is IntegerNode ||
                                          args[0] is RealNode ||
                                          args[0] is BooleanNode ||
                                          args[0] is NullNode ||
                                          args[0] is IdentifierNode;
                            return new BooleanNode(isAtom);
                        }
                        break;

                    case "islist":
                        if (args.Count == 1)
                            return new BooleanNode(args[0] is ListNode);
                        break;

                    // ---------- head ----------
                    case "head":
                        if (args.Count == 1)
                        {
                            // head '(1 2 3)
                            if (args[0] is QuoteNode q && q.Expression is ListNode qList && qList.Elements.Count > 0)
                                return qList.Elements[0];

                            // head (1 2 3)
                            if (args[0] is ListNode list && list.Elements.Count > 0)
                                return list.Elements[0];
                        }
                        break;


                    // ---------- tail ----------
                    case "tail":
                        if (args.Count == 1)
                        {
                            // tail '(1 2 3)
                            if (args[0] is QuoteNode q && q.Expression is ListNode qList && qList.Elements.Count > 0)
                            {
                                var tailItems = qList.Elements.Skip(1).ToList();
                                return new ListNode(tailItems);
                            }

                            // tail (1 2 3)
                            if (args[0] is ListNode list && list.Elements.Count > 0)
                            {
                                var tailItems = list.Elements.Skip(1).ToList();
                                return new ListNode(tailItems);
                            }
                        }
                        break;


                    // ---------- cons ----------
                    case "cons":
                        if (args.Count == 2)
                        {
                            // cons X '(1 2 3)
                            if (args[1] is QuoteNode q && q.Expression is ListNode qList)
                            {
                                var items = new List<ASTNode> { args[0] };
                                items.AddRange(qList.Elements);
                                return new ListNode(items);
                            }

                            // cons X (1 2 3)
                            if (args[1] is ListNode list)
                            {
                                var items = new List<ASTNode> { args[0] };
                                items.AddRange(list.Elements);
                                return new ListNode(items);
                            }
                        }
                        break;


                    // ---------- eval ----------
                    case "eval":
                        if (args.Count == 1)
                        {
                            // eval '(plus 1 2)
                            if (args[0] is QuoteNode q && q.Expression is ListNode inner)
                            {
                                if (inner.Elements.Count > 0 &&
                                    inner.Elements[0] is IdentifierNode innerOp)
                                {
                                    string opInner = innerOp.Name.ToLower();
                                    var innerArgs = inner.Elements.Skip(1).ToList();

                                    var evalResult = TryEvaluate(opInner, innerArgs);
                                    if (evalResult != null)
                                        return evalResult;
                                }
                            }
                        }
                        break;
                }
            }
            catch
            {
                // любая ошибка при попытке вычислить — просто считаем, что не удалось
            }

            return null;
        }

        // OPT 2: Condition Simplification
        private ASTNode SimplifyConditions(ASTNode node)
        {
            if (node is ListNode list && list.Elements.Count > 0)
            {
                if (list.Elements[0] is IdentifierNode id)
                {
                    string keyword = id.Name.ToLower();

                    if (keyword == "cond" && list.Elements.Count >= 3)
                    {
                        var condition = SimplifyConditions(list.Elements[1]);

                        // Если условие - константа, выбираем ветку
                        if (condition is BooleanNode boolNode)
                        {
                            if (boolNode.Value)
                            {
                                // Возвращаем then ветку
                                return SimplifyConditions(list.Elements[2]);
                            }
                            else
                            {
                                // Возвращаем else ветку (если есть)
                                if (list.Elements.Count > 3)
                                    return SimplifyConditions(list.Elements[3]);
                                else
                                    return new NullNode();
                            }
                        }

                        // Иначе оптимизируем рекурсивно
                        var optimized = new List<ASTNode> { list.Elements[0], condition };
                        for (int i = 2; i < list.Elements.Count; i++)
                        {
                            optimized.Add(SimplifyConditions(list.Elements[i]));
                        }
                        return new ListNode(optimized);
                    }

                    if (keyword == "while" && list.Elements.Count >= 2)
                    {
                        var condition = SimplifyConditions(list.Elements[1]);

                        // Если while(false), удаляем весь цикл
                        if (condition is BooleanNode bn && !bn.Value)
                        {
                            return null; 
                        }

                        // Иначе оптимизируем тело
                        var optimized = new List<ASTNode> { list.Elements[0], condition };
                        for (int i = 2; i < list.Elements.Count; i++)
                        {
                            optimized.Add(SimplifyConditions(list.Elements[i]));
                        }
                        return new ListNode(optimized);
                    }

                    // Для остальных - рекурсивно
                    return new ListNode(list.Elements.Select(e => SimplifyConditions(e)).ToList());
                }
            }

            return node;
        }

        // OPT 3: Unreachable Code Removal
        private ASTNode RemoveUnreachable(ASTNode node)
        {
            if (node is ListNode list && list.Elements.Count > 0)
            {
                if (list.Elements[0] is IdentifierNode id)
                {
                    string keyword = id.Name.ToLower();

                    // Для func, while, prog - удаляем код после return/break
                    if (keyword == "func" || keyword == "while" || keyword == "prog")
                    {
                        var optimized = new List<ASTNode>();
                        int bodyStart = keyword == "func" ? 3 : (keyword == "prog" ? 2 : 2);

                        // Копируем заголовок
                        for (int i = 0; i < bodyStart; i++)
                        {
                            optimized.Add(list.Elements[i]);
                        }

                        // Обрабатываем тело до return/break
                        for (int i = bodyStart; i < list.Elements.Count; i++)
                        {
                            var elem = RemoveUnreachable(list.Elements[i]);
                            optimized.Add(elem);

                            // Если это return или break, останавливаемся
                            if (elem is ListNode elemList && elemList.Elements.Count > 0)
                            {
                                if (elemList.Elements[0] is IdentifierNode elemId)
                                {
                                    string elemKeyword = elemId.Name.ToLower();
                                    if (elemKeyword == "return" || elemKeyword == "break")
                                        break;
                                }
                            }
                        }

                        return new ListNode(optimized);
                    }

                    // Для остальных - рекурсивно
                    return new ListNode(list.Elements.Select(e => RemoveUnreachable(e)).ToList());
                }
            }

            return node;
        }

        // OPT 4: Remove Unused Functions
        private ProgramNode RemoveUnusedFunctions(ProgramNode program)
        {
            var kept = new List<ASTNode>();

            foreach (var expr in program.Expressions)
            {
                if (expr is ListNode list && list.Elements.Count > 0)
                {
                    if (list.Elements[0] is IdentifierNode id && id.Name.ToLower() == "func")
                    {
                        if (list.Elements[1] is IdentifierNode funcName)
                        {
                            // Оставляем только используемые функции
                            if (usedFunctions.Contains(funcName.Name))
                            {
                                kept.Add(expr);
                            }
                        }
                    }
                    else
                    {
                        kept.Add(expr);
                    }
                }
                else
                {
                    kept.Add(expr);
                }
            }

            return new ProgramNode(kept);
        }
        private ASTNode RemoveUnusedVarsInNode(ASTNode node)
        {
            // Лист не список — возвращаем как есть
            if (node is not ListNode list || list.Elements.Count == 0)
                return node;

            // Получаем имя операции (func, setq, plus, ...)
            var head = list.Elements[0] as IdentifierNode;
            var op = head?.Name.ToLower();

            // Специальные формы, у которых есть ТЕЛО (нужно рекурсивно чистить внутри)
            if (op == "func")
            {
                // func name (params) body...
                var newElems = new List<ASTNode>();
                newElems.Add(list.Elements[0]); // func
                newElems.Add(list.Elements[1]); // name
                newElems.Add(list.Elements[2]); // params

                // Обрабатываем тело функции
                for (int i = 3; i < list.Elements.Count; i++)
                {
                    var child = RemoveUnusedVarsInNode(list.Elements[i]);
                    if (child == null)
                        continue;

                    // setq удаляем если var не используется
                    if (child is ListNode cList &&
                        cList.Elements.Count >= 3 &&
                        cList.Elements[0] is IdentifierNode id2 &&
                        id2.Name.ToLower() == "setq")
                    {
                        if (cList.Elements[1] is IdentifierNode varId &&
                            !usedVars.Contains(varId.Name))
                        {
                            // удалить
                            continue;
                        }
                    }

                    newElems.Add(child);
                }

                return new ListNode(newElems);
            }

            // Аналогичный подход для prog, lambda, while, cond
            if (op == "prog" || op == "lambda" || op == "while" || op == "cond")
            {
                var newElems = new List<ASTNode>();

                foreach (var elem in list.Elements)
                {
                    var child = RemoveUnusedVarsInNode(elem);
                    if (child != null)
                        newElems.Add(child);
                }

                return new ListNode(newElems);
            }

            // Обработка setq вне функций (на верхнем уровне)
            if (op == "setq")
            {
                if (list.Elements[1] is IdentifierNode varId &&
                    !usedVars.Contains(varId.Name))
                {
                    return null;
                }
            }

            // Для обычных вызовов — очищаем аргументы
            {
                var newElems = new List<ASTNode>();
                foreach (var elem in list.Elements)
                {
                    var child = RemoveUnusedVarsInNode(elem);
                    if (child != null)
                        newElems.Add(child);
                }
                return new ListNode(newElems);
            }
        }

        private ProgramNode RemoveUnusedVariables(ProgramNode program)
        {
            var kept = new List<ASTNode>();

            foreach (var expr in program.Expressions)
            {
                var optimized = RemoveUnusedVarsInNode(expr);
                if (optimized != null)
                    kept.Add(optimized);
            }

            return new ProgramNode(kept);
        }

        #endregion

        #region Helper Methods

        private bool IsBuiltInFunction(string name)
        {
            return name.ToLower() switch
            {
                "plus" or "minus" or "times" or "divide" or
                "equal" or "nonequal" or "less" or "lesseq" or "greater" or "greatereq" or
                "isint" or "isreal" or "isbool" or "isnull" or "isatom" or "islist" or
                "and" or "or" or "xor" or "not" or
                "head" or "tail" or "cons" or "eval" => true,
                _ => false
            };
        }

        private bool IsArithmeticOp(string op)
        {
            return op == "plus" || op == "minus" || op == "times" || op == "divide";
        }

        private bool IsComparisonOp(string op)
        {
            return op == "less" || op == "lesseq" || op == "greater" ||
                   op == "greatereq" || op == "equal" || op == "nonequal";
        }

        private bool IsLogicalOp(string op)
        {
            return op == "and" || op == "or" || op == "not";
        }

        private double? GetNumber(ASTNode node)
        {
            return node switch
            {
                IntegerNode i => i.Value,
                RealNode r => r.Value,
                _ => null
            };
        }

        private bool IsWholeNumber(double value)
        {
            return Math.Abs(value - Math.Round(value)) < 0.000001;
        }

        private void PrintResults()
        {
            Console.WriteLine($"Errors: {errors.Count}");
            foreach (var error in errors)
                Console.WriteLine($"  ERROR: {error}");

            Console.WriteLine($"Warnings: {warnings.Count}");
            foreach (var warning in warnings)
                Console.WriteLine($"  WARNING: {warning}");

            if (HasErrors)
            {
                Console.WriteLine("Errors detected:");
                foreach (var error in errors)
                    Console.WriteLine($"  ERROR: {error}");
            }
        }

        #endregion
    }
}