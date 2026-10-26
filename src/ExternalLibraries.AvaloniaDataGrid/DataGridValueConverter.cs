// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Controls.Utils;
using Avalonia.Data.Converters;
using Avalonia.Utilities;

namespace Avalonia.Controls
{
    internal class DataGridValueConverter : IValueConverter
    {
        public static DataGridValueConverter Instance = new DataGridValueConverter();

        private static object? ConvertValue(object? value, Type targetType, CultureInfo culture)
        {
            if (value is null || targetType.IsInstanceOfType(value)) return value;
            if (targetType == typeof(string))
                return value is IFormattable formattable ? formattable.ToString(null, culture) : value.ToString();
            // Explicit converters are required for non-display conversions.
            return Avalonia.Data.BindingOperations.DoNothing;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return ConvertValue(value, targetType, culture);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (targetType.IsNullableType())
            {
                var strValue = value as string;

                // This suppresses a warning saying that we should use String.IsNullOrEmpty instead of a string
                // comparison, but in this case we want to explicitly check for Empty and not Null.
#pragma warning disable CA1820
                if (strValue == string.Empty)
#pragma warning restore CA1820
                {
                    return null;
                }
            }
            return ConvertValue(value, targetType, culture);
        }
    }
}
