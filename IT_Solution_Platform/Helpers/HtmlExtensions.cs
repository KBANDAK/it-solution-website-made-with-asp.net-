using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;

namespace IT_Solution_Platform.Helpers
{
    public static class HtmlExtensions
    {
        public static SelectList GetEnumSelectList<TEnum>(this HtmlHelper htmlHelper) where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum)
            {
                throw new ArgumentException("TEnum must be an enumerated type");
            }

            var values = from TEnum e in Enum.GetValues(typeof(TEnum))
                         select new
                         {
                             Value = Convert.ToInt32(e),
                             Text = GetDisplayName(e as Enum)
                         };

            return new SelectList(values, "Value", "Text");
        }

        private static string GetDisplayName(Enum enumValue)
        {
            return enumValue.GetType()
                           .GetMember(enumValue.ToString())
                           .FirstOrDefault()
                           ?.GetCustomAttribute<DisplayAttribute>()
                           ?.GetName() ?? enumValue.ToString();
        }


        public static string ToTitleCase(this string input)
        { 
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            // Insert spaces before capital letters (except the first)
            string spaced = System.Text.RegularExpressions.Regex.Replace(input, "(?<!^)([A-Z])", " $1");

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
        }
    }
}