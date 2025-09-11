using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F_Interpretator
{
    public record Token(TokenType tokenType, object literal)
    {
        public override string ToString()
        {
            string formattedLiteral = literal switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                decimal m => m.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                _ => literal?.ToString() ?? "null"
            };

            return string.Format("Token {{ type: {0}, literal: \"{1}\" }}", tokenType, formattedLiteral);
        }
    }

}