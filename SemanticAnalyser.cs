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

        // NEW: track variables which hold lambdas (name -> arity)
        private Dictionary<string, int> functionVariables = new Dictionary<string, int>();
        // NEW: stack of parameter name sets to allow calling params as functions
        private Stack<HashSet<string>> parameterScopes = new Stack<HashSet<string>>();

        public bool HasErrors => errors.Count > 0;
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        public ProgramNode Analyze(ProgramNode program)
        {
            //Console.WriteLine("=== Semantic Analysis Started ===");
            variableScopes.Push(new HashSet<string>());
            parameterScopes.Push(new HashSet<string>()); // global empty

            // Phase 1: Collect declared functions (so identifiers that are function names are known)
            CollectFunctions(program);

            // Phase 2: Checks
            PerformChecks(program);

            // Phase 3: Optimizations (only if no errors)
            var optimizedProgram = program;
            if (!HasErrors)
            {
                optimizedProgram = ApplyOptimizations(program);
            }

            //PrintResults();
            return optimizedProgram;
        }

        #region Phase 1: Collect Functions

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

        #region Phase 2: Checks

        private void PerformChecks(ProgramNode program)
        {
            foreach (var expr in program.Expressions)
            {
                CheckNode(expr);
            }

            // CHECK 4: Unused variables -> warnings
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

                    // Special forms
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
                            // CHECK: break only in loops
                            if (loopDepth == 0)
                                errors.Add("'break' can only be used inside a loop");
                            return;
                        case "quote":
                            // quote content is literal, do not traverse into it for variable usage
                            return;
                        default:
                            // function call or arbitrary list
                            CheckFunctionCall(list);
                            return;
                    }
                }
                else
                {
                    // Head is not an identifier (e.g., nested call like ((myfunc -1) 1 2))
                    // This is handled in CheckFunctionCall, but we still need to route it there
                    CheckFunctionCall(list);
                    return;
                }
            }

            // Identifier usage: mark as used variable or used function name if it's a function identifier
            if (node is IdentifierNode identifier)
            {
                var name = identifier.Name;

                // If it's a declared function name and appears as an identifier (e.g. passed as argument),
                // mark function as used (so it won't be removed).
                if (declaredFunctions.ContainsKey(name))
                {
                    usedFunctions.Add(name);
                }
                else if (IsVariableDeclared(name) || functionVariables.ContainsKey(name))
                {
                    usedVars.Add(name);
                }
                // else: unknown identifier, ignore here (other checks handle undeclared calls)
            }
        }
        private void CheckFunc(ListNode list)
        {
            // (func name (params) body...)

            // ������� ��� ���������������� � Phase 1
            /*
            if (list.Elements.Count >= 3 && list.Elements[1] is IdentifierNode funcName && list.Elements[2] is ListNode paramList1)
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
            */

            if (list.Elements.Count < 4)
            {
                errors.Add("func requires at least: name, parameter list, and body");
                return;
            }

            if (list.Elements[2] is ListNode paramList)
            {
                functionDepth++;
                var localVars = new HashSet<string>();
                var paramNames = new HashSet<string>();

                // Add parameters as local variables and mark in param scope
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                    {
                        localVars.Add(paramId.Name);
                        declaredVars.Add(paramId.Name);
                        paramNames.Add(paramId.Name);
                    }
                }

                variableScopes.Push(localVars);
                parameterScopes.Push(paramNames);

                // Check function body
                for (int i = 3; i < list.Elements.Count; i++)
                {
                    CheckNode(list.Elements[i]);
                }

                parameterScopes.Pop();
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

            if (!(list.Elements[1] is IdentifierNode varName))
            {
                errors.Add("First argument of setq must be an identifier");
                return;
            }

            // First check RHS (value)
            var valueNode = list.Elements[2];
            CheckNode(valueNode);

            // After checking RHS, register the variable in current scope
            declaredVars.Add(varName.Name);
            if (variableScopes.Count > 0)
                variableScopes.Peek().Add(varName.Name);

            // If RHS is a lambda form, mark variable as function-variable with arity
            if (valueNode is ListNode valueList &&
                valueList.Elements.Count > 0 &&
                valueList.Elements[0] is IdentifierNode headId &&
                headId.Name.ToLower() == "lambda")
            {
                if (valueList.Elements.Count >= 2 && valueList.Elements[1] is ListNode paramList)
                {
                    int paramCount = paramList.Elements.Count;
                    functionVariables[varName.Name] = paramCount;
                }
            }
            // If RHS is a function call, mark as function-variable (unknown arity)
            else if (valueNode is ListNode callList && callList.Elements.Count > 0 && callList.Elements[0] is IdentifierNode callHead && declaredFunctions.ContainsKey(callHead.Name))
            {
                functionVariables[varName.Name] = -1; // -1 means unknown arity
            }
            else
            {
                // If previously marked as function variable, but now assigned non-function -> remove mark
                if (functionVariables.ContainsKey(varName.Name))
                    functionVariables.Remove(varName.Name);
            }
        }

        private bool IsVariableDeclared(string name)
        {
            // check across variableScopes stack
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

            // condition
            CheckNode(list.Elements[1]);

            // body
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
            var paramNames = new HashSet<string>();

            if (list.Elements[1] is ListNode paramList)
            {
                foreach (var param in paramList.Elements)
                {
                    if (param is IdentifierNode paramId)
                    {
                        localVars.Add(paramId.Name);
                        declaredVars.Add(paramId.Name);
                        paramNames.Add(paramId.Name);
                    }
                }
            }

            variableScopes.Push(localVars);
            parameterScopes.Push(paramNames);

            // Lambda body (in original parser lambda had single body node)
            CheckNode(list.Elements[2]);

            parameterScopes.Pop();
            variableScopes.Pop();
            functionDepth--;
        }

        private void CheckReturn(ListNode list)
        {
            // return only inside functions
            if (functionDepth == 0)
                errors.Add("'return' can only be used inside a function");

            if (list.Elements.Count == 2)
            {
                CheckNode(list.Elements[1]);
            }
        }

        private void CheckFunctionCall(ListNode list)
        {
            // NEW: Handle case where head is itself a list (like ((myfunc -1) 1 2))
            if (list.Elements[0] is ListNode innerCall)
            {
                // Recursively check the inner call
                CheckNode(innerCall);

                // Check all arguments
                for (int i = 1; i < list.Elements.Count; i++)
                {
                    CheckNode(list.Elements[i]);
                }
                return;
            }

            if (list.Elements[0] is IdentifierNode funcId)
            {
                string funcName = funcId.Name;
                string funcNameLower = funcName.ToLower();

                // If built-in -> we don't require declaration
                if (!IsBuiltInFunction(funcNameLower))
                {
                    if (declaredFunctions.ContainsKey(funcName))
                    {
                        usedFunctions.Add(funcName);
                        int expectedParams = declaredFunctions[funcName];
                        int actualParams = list.Elements.Count - 1;
                        if (expectedParams != actualParams)
                        {
                            errors.Add($"Function '{funcName}' expects {expectedParams} parameter(s) but got {actualParams}");
                        }
                    }
                    else if (IsVariableDeclared(funcName))
                    {
                        // If var is known to hold a lambda or function call, check arity if known
                        if (functionVariables.TryGetValue(funcName, out int varFuncArity))
                        {
                            usedVars.Add(funcName);
                            int actualParams = list.Elements.Count - 1;
                            if (varFuncArity >= 0 && varFuncArity != actualParams)
                            {
                                errors.Add($"Function-variable '{funcName}' expects {varFuncArity} parameter(s) but got {actualParams}");
                            }
                            // If arity is unknown (-1), allow call without error
                        }
                        else
                        {
                            // Allow call if variable is assigned from a function, even if arity is unknown
                            usedVars.Add(funcName);
                        }
                    }
                    else
                    {
                        errors.Add($"Function '{funcName}' is not declared");
                    }
                }

                // recursively check arguments (this will mark usage of identifiers inside args)
                for (int i = 1; i < list.Elements.Count; i++)
                {
                    CheckNode(list.Elements[i]);
                }
            }
        }
        #endregion

        #region Phase 3: Optimizations

        private ProgramNode ApplyOptimizations(ProgramNode program)
        {
            // OPT 1: Constant folding
            var afterConstFolding = new ProgramNode(
                program.Expressions.Select(e => FoldConstants(e)).Where(e => e != null).ToList());

            // OPT 2: Condition simplification
            var afterCondSimplify = new ProgramNode(
                afterConstFolding.Expressions.Select(e => SimplifyConditions(e)).Where(e => e != null).ToList());

            // OPT 3: Unreachable code removal
            var afterUnreachable = new ProgramNode(
                afterCondSimplify.Expressions.Select(e => RemoveUnreachable(e)).Where(e => e != null).ToList());

            // OPT 3.5: Unused vars removal
            var afterUnusedVarsRemoval = RemoveUnusedVariables(afterUnreachable);

            // OPT 4: Unused function removal
            var afterUnusedRemoval = RemoveUnusedFunctions(afterUnusedVarsRemoval);

            return afterUnusedRemoval;
        }

        // OPT 1: Constant Expression Folding
        private ASTNode FoldConstants(ASTNode node)
        {
            // If node is list
            if (node is ListNode list && list.Elements.Count > 0)
            {
                // If it's a quote form - preserve as-is
                if (list.Elements[0] is IdentifierNode id0 && id0.Name.ToLower() == "quote")
                    return list;

                // If it's a special form, recursively fold children but don't evaluate whole form
                var headName = (list.Elements[0] as IdentifierNode)?.Name.ToLower();
                var specialForms = new HashSet<string> { "func", "while", "prog", "lambda", "cond", "return", "break", "quote" };
                if (headName != null && specialForms.Contains(headName))
                {
                    var elems = list.Elements.Select(e => FoldConstants(e)).Where(e => e != null).ToList();
                    return new ListNode(elems);
                }

                // general case: fold children
                var optimizedElements = list.Elements.Select(e => FoldConstants(e)).Where(e => e != null).ToList();

                // try to evaluate if operator + all args are foldable to constants
                var op = (optimizedElements[0] as IdentifierNode)?.Name.ToLower();
                if (op != null)
                {
                    var args = optimizedElements.Skip(1).ToList();
                    var result = TryEvaluate(op, args);
                    if (result != null)
                        return result;
                }

                return new ListNode(optimizedElements);
            }

            // otherwise unchanged
            return node;
        }

        private ASTNode TryEvaluate(string op, List<ASTNode> args)
        {
            try
            {
                switch (op)
                {
                    // Arithmetic (2 args)
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
                            if (v1.HasValue && v2.HasValue && Math.Abs(v2.Value) > 1e-9)
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

                    // Logical (fold only for boolean literal args)
                    case "and":
                        if (args.Count == 2 && args[0] is BooleanNode bb1 && args[1] is BooleanNode bb2)
                            return new BooleanNode(bb1.Value && bb2.Value);
                        break;

                    case "or":
                        if (args.Count == 2 && args[0] is BooleanNode bb3 && args[1] is BooleanNode bb4)
                            return new BooleanNode(bb3.Value || bb4.Value);
                        break;

                    case "not":
                        if (args.Count == 1 && args[0] is BooleanNode bb5)
                            return new BooleanNode(!bb5.Value);
                        break;

                    // Predicates: fold only if argument is an immediate literal, NOT an identifier
                    case "isint":
                        if (args.Count == 1)
                        {
                            // Don't fold if it's a variable - we don't know its runtime value
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is IntegerNode) return new BooleanNode(true);
                            if (args[0] is RealNode || args[0] is BooleanNode || args[0] is NullNode || args[0] is ListNode || args[0] is QuoteNode)
                                return new BooleanNode(false);
                        }
                        break;

                    case "isreal":
                        if (args.Count == 1)
                        {
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is RealNode) return new BooleanNode(true);
                            if (args[0] is IntegerNode || args[0] is BooleanNode || args[0] is NullNode || args[0] is ListNode || args[0] is QuoteNode)
                                return new BooleanNode(false);
                        }
                        break;

                    case "isbool":
                        if (args.Count == 1)
                        {
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is BooleanNode) return new BooleanNode(true);
                            if (args[0] is IntegerNode || args[0] is RealNode || args[0] is NullNode || args[0] is ListNode || args[0] is QuoteNode)
                                return new BooleanNode(false);
                        }
                        break;

                    case "isnull":
                        if (args.Count == 1)
                        {
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is NullNode) return new BooleanNode(true);
                            if (!(args[0] is NullNode)) return new BooleanNode(false);
                        }
                        break;

                    case "isatom":
                        if (args.Count == 1)
                        {
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is IntegerNode || args[0] is RealNode || args[0] is BooleanNode || args[0] is NullNode)
                                return new BooleanNode(true);
                            if (args[0] is ListNode || args[0] is QuoteNode)
                                return new BooleanNode(false);
                        }
                        break;

                    case "islist":
                        if (args.Count == 1)
                        {
                            if (args[0] is IdentifierNode) return null;

                            if (args[0] is ListNode) return new BooleanNode(true);
                            if (args[0] is QuoteNode q && q.Expression is ListNode) return new BooleanNode(true);
                            if (args[0] is IntegerNode || args[0] is RealNode || args[0] is BooleanNode || args[0] is NullNode)
                                return new BooleanNode(false);
                        }
                        break;

                    // head/tail/cons fold only for quoted constant lists or literal ListNode
                    case "head":
                        if (args.Count == 1)
                        {
                            if (args[0] is QuoteNode q && q.Expression is ListNode qList && qList.Elements.Count > 0)
                                return qList.Elements[0];
                            if (args[0] is ListNode real && real.Elements.Count > 0)
                                return real.Elements[0];
                        }
                        break;

                    case "tail":
                        if (args.Count == 1)
                        {
                            if (args[0] is QuoteNode q && q.Expression is ListNode qList && qList.Elements.Count > 0)
                            {
                                var tailItems = qList.Elements.Skip(1).ToList();
                                return new ListNode(tailItems);
                            }
                            if (args[0] is ListNode real && real.Elements.Count > 0)
                            {
                                var tailItems = real.Elements.Skip(1).ToList();
                                return new ListNode(tailItems);
                            }
                        }
                        break;

                    case "cons":
                        if (args.Count == 2)
                        {
                            if (args[1] is QuoteNode q && q.Expression is ListNode qList)
                            {
                                var items = new List<ASTNode> { args[0] };
                                items.AddRange(qList.Elements);
                                return new ListNode(items);
                            }
                            if (args[1] is ListNode real)
                            {
                                var items = new List<ASTNode> { args[0] };
                                items.AddRange(real.Elements);
                                return new ListNode(items);
                            }
                        }
                        break;

                    case "eval":
                        if (args.Count == 1)
                        {
                            if (args[0] is QuoteNode q && q.Expression is ListNode inner && inner.Elements.Count > 0 && inner.Elements[0] is IdentifierNode innerOp)
                            {
                                string opInner = innerOp.Name.ToLower();
                                var innerArgs = inner.Elements.Skip(1).ToList();
                                var innerResult = TryEvaluate(opInner, innerArgs);
                                if (innerResult != null) return innerResult;
                            }
                        }
                        break;
                }
            }
            catch { }

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

                        // If condition is constant boolean -> choose branch
                        if (condition is BooleanNode boolNode)
                        {
                            if (boolNode.Value)
                            {
                                return SimplifyConditions(list.Elements[2]);
                            }
                            else
                            {
                                if (list.Elements.Count > 3)
                                    return SimplifyConditions(list.Elements[3]);
                                else
                                    return new NullNode();
                            }
                        }

                        // else recursively simplify
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

                        if (condition is BooleanNode bn && !bn.Value)
                        {
                            // remove whole loop
                            return null;
                        }

                        var optimized = new List<ASTNode> { list.Elements[0], condition };
                        for (int i = 2; i < list.Elements.Count; i++)
                        {
                            optimized.Add(SimplifyConditions(list.Elements[i]));
                        }
                        return new ListNode(optimized);
                    }

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

                    if (keyword == "func" || keyword == "while" || keyword == "prog")
                    {
                        var optimized = new List<ASTNode>();
                        int bodyStart = keyword == "func" ? 3 : (keyword == "prog" ? 2 : 2);

                        for (int i = 0; i < bodyStart; i++)
                            optimized.Add(list.Elements[i]);

                        for (int i = bodyStart; i < list.Elements.Count; i++)
                        {
                            var elem = RemoveUnreachable(list.Elements[i]);
                            if (elem != null)
                                optimized.Add(elem);

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

                    return new ListNode(list.Elements.Select(e => RemoveUnreachable(e)).Where(e => e != null).ToList());
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
                            // Keep only used functions (usedFunctions filled earlier)
                            if (usedFunctions.Contains(funcName.Name))
                            {
                                kept.Add(expr);
                            }
                            else
                            {
                                // But if function's name appears as identifier anywhere (passed as arg) it will be in usedFunctions.
                                // So here it's safe to drop truly unused functions.
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
            if (node is not ListNode list || list.Elements.Count == 0)
                return node;

            var head = list.Elements[0] as IdentifierNode;
            var op = head?.Name.ToLower();

            if (op == "func")
            {
                var newElems = new List<ASTNode>();
                newElems.Add(list.Elements[0]); // func
                newElems.Add(list.Elements[1]); // name
                newElems.Add(list.Elements[2]); // params

                for (int i = 3; i < list.Elements.Count; i++)
                {
                    var child = RemoveUnusedVarsInNode(list.Elements[i]);
                    if (child == null) continue;

                    // drop setq of unused var inside function
                    if (child is ListNode cList &&
                        cList.Elements.Count >= 3 &&
                        cList.Elements[0] is IdentifierNode id2 &&
                        id2.Name.ToLower() == "setq")
                    {
                        if (cList.Elements[1] is IdentifierNode varId &&
                            !usedVars.Contains(varId.Name))
                        {
                            continue; // drop it
                        }
                    }

                    newElems.Add(child);
                }

                return new ListNode(newElems);
            }

            if (op == "prog" || op == "lambda" || op == "while" || op == "cond")
            {
                var newElems = new List<ASTNode>();
                foreach (var elem in list.Elements)
                {
                    var child = RemoveUnusedVarsInNode(elem);
                    if (child != null) newElems.Add(child);
                }
                return new ListNode(newElems);
            }

            if (op == "setq")
            {
                if (list.Elements[1] is IdentifierNode varId &&
                    !usedVars.Contains(varId.Name))
                {
                    return null;
                }
            }

            var newElements = new List<ASTNode>();
            foreach (var elem in list.Elements)
            {
                var child = RemoveUnusedVarsInNode(elem);
                if (child != null) newElements.Add(child);
            }
            return new ListNode(newElements);
        }

        private ProgramNode RemoveUnusedVariables(ProgramNode program)
        {
            var kept = new List<ASTNode>();
            foreach (var expr in program.Expressions)
            {
                var optimized = RemoveUnusedVarsInNode(expr);
                if (optimized != null) kept.Add(optimized);
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
