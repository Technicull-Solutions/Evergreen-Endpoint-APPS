// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Avalonia.Controls.Utils
{
    internal static class TypeHelper
    {
        internal const char LeftIndexerToken = '[';
        internal const char PropertyNameSeparator = '.';
        internal const char RightIndexerToken = ']';
        internal const char LeftParenthesisToken = '(';
        internal const char RightParenthesisToken = ')';

        /// <summary>
        /// Gets the default member name that is used for an indexer (e.g. "Item").
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>Default member name.</returns>
        private static string? GetDefaultMemberName(this Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(DefaultMemberAttribute), true);
            if (attributes != null && attributes.Length == 1)
            {
                DefaultMemberAttribute defaultMemberAttribute = (DefaultMemberAttribute)attributes[0];
                return defaultMemberAttribute.MemberName;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the PropertyInfo for the specified property path within this Type, and returns
        /// the value of GetShortName on its DisplayAttribute, if one exists. GetShortName will return
        /// the value of Name if there is no ShortName specified.
        /// </summary>
        /// <param name="type">Type to search</param>
        /// <param name="propertyPath">property path</param>
        /// <returns>DisplayAttribute.ShortName if it exists, null otherwise</returns>
        internal static string? GetDisplayName(this Type type, string propertyPath)
        {
            PropertyInfo? propertyInfo = type.GetNestedProperty(propertyPath);
            if (propertyInfo != null)
            {
                object[] attributes = propertyInfo.GetCustomAttributes(typeof(DisplayAttribute), true);
                if (attributes != null && attributes.Length > 0)
                {
                    Debug.Assert(attributes.Length == 1);
                    if (attributes[0] is DisplayAttribute displayAttribute)
                    {
                        return displayAttribute.GetShortName();
                    }
                }
            }
            return null;
        }

        internal static Type GetEnumerableItemType(this Type enumerableType)
        {
            _ = enumerableType;
            // The display contract is expressed by compiled column bindings, not discovered properties.
            return typeof(object);
        }

        /// <summary>
        /// Retrieves the value and type of a property. That property can be nested and its path
        /// can include indexers. Each element of the path needs to be a public instance property.
        /// </summary>
        /// <param name="parentType">The parent Type</param>
        /// <param name="propertyPath">Property path</param>
        /// <param name="exception">Potential exception</param>
        /// <param name="item">Parent item which will be set to the property value if non-null.</param>
        /// <returns></returns>
        private static PropertyInfo? GetNestedProperty(this Type? parentType, string? propertyPath, out Exception? exception, ref object? item)
        {
            exception = null;
            if (parentType == null || String.IsNullOrEmpty(propertyPath))
            {
                item = null;
                return null;
            }

            Type type = parentType;
            PropertyInfo? propertyInfo = null;
            List<string> propertyNames = SplitPropertyPath(propertyPath);
            for (int i = 0; i < propertyNames.Count; i++)
            {
                // if we can't find the property or it is not of the correct type,
                // treat it as a null value
                propertyInfo = type.GetPropertyOrIndexer(propertyNames[i], out object[]? index);
                if (propertyInfo == null)
                {
                    item = null;
                    return null;
                }

                if (!propertyInfo.CanRead)
                {
                    exception =
                        new InvalidOperationException(
                            $"The property named '{propertyNames[i]}' on type '{type.GetTypeName()}' cannot be read.");
                    item = null;
                    return null;
                }

                if (item != null)
                {
                    item = propertyInfo.GetValue(item, index);
                }
                type = propertyInfo.PropertyType.GetNonNullableType();
            }

            return propertyInfo;
        }

        /// <summary>
        /// Finds the leaf PropertyInfo for the specified property path, and returns its value
        /// if the item is non-null.
        /// </summary>
        /// <param name="parentType">Type to search.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="item">Parent item which will be set to the property value if non-null.</param>
        /// <returns>The PropertyInfo.</returns>
        internal static PropertyInfo? GetNestedProperty(this Type parentType, string? propertyPath, ref object? item)
        {
            return parentType.GetNestedProperty(propertyPath, out _, ref item);
        }

        internal static PropertyInfo? GetNestedProperty(this Type? parentType, string? propertyPath)
        {
            if (parentType != null)
            {
                object? item = null;
                return parentType.GetNestedProperty(propertyPath, ref item);
            }
            return null;
        }

        /// <summary>
        /// Returns the friendly name for a type
        /// </summary>
        /// <param name="type">The type to get the name from</param>
        /// <returns>Textual representation of the input type</returns>
        internal static string GetTypeName(this Type type)
        {
            Type baseType = type.GetNonNullableType();
            string s = baseType.Name;
            if (type != baseType)
            {
                s += '?';
            }
            return s;
        }

        internal static Type? GetNestedPropertyType(this Type? parentType, string? propertyPath)
        {
            if (parentType == null || String.IsNullOrEmpty(propertyPath))
            {
                return parentType;
            }

            PropertyInfo? propertyInfo = parentType.GetNestedProperty(propertyPath);
            if (propertyInfo != null)
            {
                return propertyInfo.PropertyType;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the value of a property. That property can be nested and its path can
        /// include indexers. Each element of the path needs to be a public instance property.
        /// The return value will either be of type propertyType or it will be null.
        /// </summary>
        /// <param name="item">Object that exposes the property</param>
        /// <param name="propertyPath">Property path</param>
        /// <param name="propertyType">Property type</param>
        /// <param name="exception">Potential exception</param>
        /// <returns>Property value</returns>
        internal static object? GetNestedPropertyValue(object? item, string? propertyPath, Type propertyType, out Exception? exception)
        {
            exception = null;

            // if the item is null, treat the property value as null
            if (item == null)
            {
                return null;
            }

            // if the propertyPath is null or empty, return the item
            if (String.IsNullOrEmpty(propertyPath))
            {
                return item;
            }

            object? propertyValue = item;
            Type? itemType = item.GetType();
            if (itemType != null)
            {
                PropertyInfo? propertyInfo = itemType.GetNestedProperty(propertyPath, out exception, ref propertyValue);
                if (propertyInfo != null && propertyInfo.PropertyType != propertyType)
                {
                    return null;
                }
            }
            return propertyValue;
        }

        /// <summary>
        /// Gets the value of a given property path on a particular data item.
        /// </summary>
        /// <param name="item">Parent data item.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <returns>Value.</returns>
        internal static object? GetNestedPropertyValue(object? item, string? propertyPath)
        {
            if (item != null)
            {
                Type parentType = item.GetType();
                if (String.IsNullOrEmpty(propertyPath))
                {
                    return item;
                }
                else if (parentType != null)
                {
                    object? nestedValue = item;
                    parentType.GetNestedProperty(propertyPath, ref nestedValue);
                    return nestedValue;
                }
            }
            return null;
        }

        internal static Type GetNonNullableType(this Type type)
        {
            if (IsNullableType(type))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        /// <summary>
        /// Returns the PropertyInfo for the specified property path.  If the property path
        /// refers to an indexer (e.g. "[abc]"), then the index out parameter will be set to the value
        /// specified in the property path.  This method only supports indexers with a single parameter
        /// that is either an int or a string.  Int parameters take priority over string parameters.
        /// </summary>
        /// <param name="type">Type to search.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="index">Set to the index if return value is an indexer, otherwise null.</param>
        /// <returns>PropertyInfo for either a property or an indexer.</returns>
        internal static PropertyInfo? GetPropertyOrIndexer(this Type type, string? propertyPath, out object[]? index)
        {
            _ = propertyPath;
            _ = type;
            // Name-based property discovery is unavailable in the Evergreen grid.
            // Compiled bindings and explicit sort comparers provide the supported paths.
            index = null;
            return null;
        }

        internal static bool IsEnumerableType(this Type enumerableType)
        {
            return typeof(System.Collections.IEnumerable).IsAssignableFrom(enumerableType);
        }

        internal static bool IsNullableType(this Type type)
        {
            return (((type != null) && type.IsGenericType) && (type.GetGenericTypeDefinition() == typeof(Nullable<>)));
        }

        internal static bool IsNullableEnum(this Type type)
        {
            return type.IsNullableType() &&
                 type.GetGenericArguments().Length == 1 &&
                 type.GetGenericArguments()[0].IsEnum;
        }

        /// <summary>
        /// If the specified property is an indexer, this method will prepend the object's
        /// default member name to it (e.g. "[foo]" returns "Item[foo]").
        /// </summary>
        /// <param name="item">Declaring data item.</param>
        /// <param name="property">Property name.</param>
        /// <returns>Property with default member name prepended, or property if unchanged.</returns>
        internal static string? PrependDefaultMemberName(object? item, string? property)
        {
            if (item != null && !string.IsNullOrEmpty(property) && property![0] == TypeHelper.LeftIndexerToken)
            {
                // The leaf property name is an indexer, so add the default member name.
                Type? declaringType = item.GetType();
                if (declaringType != null)
                {
                    string? defaultMemberName = declaringType.GetNonNullableType().GetDefaultMemberName();
                    if (!string.IsNullOrEmpty(defaultMemberName))
                    {
                        return defaultMemberName + property;
                    }
                }
            }
            return property;
        }

        /// <summary>
        /// If the specified property is an indexer, this method will remove the object's
        /// default member name from it (e.g. "Item[foo]" returns "[foo]").
        /// </summary>
        /// <param name="property">Property name.</param>
        /// <returns>Property with default member name removed, or property if unchanged.</returns>
        internal static string RemoveDefaultMemberName(string property)
        {
            if (!string.IsNullOrEmpty(property) && property[property.Length - 1] == TypeHelper.RightIndexerToken)
            {
                // The property is an indexer, so remove the default member name.
                int leftIndexerToken = property.IndexOf(TypeHelper.LeftIndexerToken);
                if (leftIndexerToken >= 0)
                {
                    return property.Substring(leftIndexerToken);
                }
            }
            return property;
        }

        /// <summary>
        /// Returns a list of substrings where each one represents a single property within a nested
        /// property path which may include indexers.  For example, the string "abc.d[efg][h].ijk"
        /// would return the substrings: "abc", "d", "[efg]", "[h]", and "ijk".
        /// </summary>
        /// <param name="propertyPath">Path to split.</param>
        /// <returns>List of property substrings.</returns>
        internal static List<string> SplitPropertyPath(string? propertyPath)
        {
            List<string> propertyPaths = new List<string>();
            if (!string.IsNullOrEmpty(propertyPath))
            {
                bool parenthesisOn = false;
                int startIndex = 0;
                for (int index = 0; index < propertyPath!.Length; index++)
                {
                    if (parenthesisOn)
                    {
                        if (propertyPath[index] == RightParenthesisToken)
                        {
                            parenthesisOn = false;
                            startIndex = index + 1;
                        }
                        continue;
                    }

                    if (propertyPath[index] == LeftParenthesisToken)
                    {
                        parenthesisOn = true;
                        if (startIndex != index)
                        {
                            propertyPaths.Add(propertyPath.Substring(startIndex, index - startIndex));
                            startIndex = index + 1;
                        }
                    }
                    else if (propertyPath[index] == PropertyNameSeparator)
                    {
                        if (startIndex != index)
                        {
                            propertyPaths.Add(propertyPath.Substring(startIndex, index - startIndex));
                        }
                        startIndex = index + 1;
                    }
                    else if (startIndex != index && propertyPath[index] == LeftIndexerToken)
                    {
                        propertyPaths.Add(propertyPath.Substring(startIndex, index - startIndex));
                        startIndex = index;
                    }
                    else if (index == propertyPath.Length - 1)
                    {
                        propertyPaths.Add(propertyPath.Substring(startIndex));
                    }
                }
            }
            return propertyPaths;
        }

        /// <summary>
        /// Checks a MemberInfo object (e.g. a Type or PropertyInfo) for the ReadOnly attribute
        /// and returns the value of IsReadOnly if it exists.
        /// </summary>
        /// <param name="memberInfo">MemberInfo to check</param>
        /// <returns>true if MemberInfo is read-only, false otherwise</returns>
        internal static bool GetIsReadOnly(this MemberInfo? memberInfo)
        {
            if (memberInfo != null)
            {
                // Check if ReadOnlyAttribute is defined on the member
                object[] attributes = memberInfo.GetCustomAttributes(typeof(ReadOnlyAttribute), true);
                if (attributes != null && attributes.Length > 0)
                {
                    ReadOnlyAttribute readOnlyAttribute = (ReadOnlyAttribute)attributes[0];
                    return readOnlyAttribute.IsReadOnly;
                }
            }
            return false;
        }

        internal static Type? GetItemType(this IEnumerable list)
        {
            Type listType = list.GetType();
            Type? itemType = null;

            // if it's a generic enumerable, we get the generic type

            // Unfortunately, if data source is fed from a bare IEnumerable, TypeHelper will report an element type of object,
            // which is not particularly interesting.  We deal with it further on.
            if (listType.IsEnumerableType())
            {
                itemType = listType.GetEnumerableItemType();
            }

            // Bare IEnumerables mean that result type will be object.  In that case, we try to get something more interesting
            if (itemType == null || itemType == typeof(object))
            {
                // We haven't located a type yet.. try a different approach.
                // Does the list have anything in it?

                IEnumerator en = list.GetEnumerator();
                if (en.MoveNext() && en.Current != null)
                {
                    return en.Current.GetType();
                }
            }

            // if we're null at this point, give up
            return itemType;
        }
    }
}
