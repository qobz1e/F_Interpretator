using System;


namespace F_Interpretator
{
    public enum TokenType
    {
        Program,
        List,
        Element,
        Atom,
        Literal,
        Identifier,
        Letter,
        Integer,
        DecimalDigit,
        Real,
        Boolean,

        Keyword,
        True,
        False,
        OpenParenthesis,
        CloseParenthesis,

        plus_Token,
        minus_Token,
        times_Token,
        divide_Token,

        equal_Token,
        nonequal_Token,
        less_Token,
        lesseq_Token,
        greater_Token,
        greatereq_Token,

        isint_Token,
        isreal_Token,
        isbool_Token,
        isnull_Token,
        isatom_Token,
        islist_Token,

        and_Token,
        or_Token,
        xor_Token,
        not_Token,

        head_Token,
        tail_Token,
        cons_Token,

        eval_Token,
        quote_Token,
        setq_Token,
        func_Token,
        lambda_Token,
        prog_Token,
        cond_Token,
        while_Token,
        return_Token,
        break_Token,
        value_Token,

        EndLine,
        EOF
    }
}