using System;
using System.Globalization;


namespace F_Interpretator
{
    public record Token(TokenType tokenType, object literal)
    {
        public override string ToString()
        {
            string formattedLiteral = literal switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                _ => literal?.ToString() ?? "null"
            };

            return string.Format("Token {{ type: {0}, literal: \"{1}\" }}", tokenType, formattedLiteral);
        }
    }

}