/*
// <copyright>
// dotNetRDF is free and open source software licensed under the MIT License
// -------------------------------------------------------------------------
// 
// Copyright (c) 2009-2024 dotNetRDF Project (http://dotnetrdf.org/)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Query.Expressions;

namespace VDS.RDF.Query;

// TODO: This class should take a SparqlNodeComparer instance as a constructor parameter and only implement handling the error states.

/// <summary>
/// Comparer class for use in SPARQL ORDER BY - implements the Semantics broadly similar to the relational operator but instead of erroring using Node/Lexical ordering where an error would occur it makes an appropriate decision.
/// </summary>
public class SparqlOrderingComparer 
    : SparqlNodeComparer, IComparer<INode>, IComparer<IValuedNode>
{
    /// <summary>
    /// Create a new comparer that uses the same culture and comparison options as the specified node comparer.
    /// </summary>
    /// <param name="nodeComparer">The node comparer whose culture and comparison options are to be used.</param>
    public SparqlOrderingComparer(ISparqlNodeComparer nodeComparer):base(nodeComparer?.Culture ?? CultureInfo.InvariantCulture, nodeComparer?.Options ?? CompareOptions.Ordinal) {}

    /// <summary>
    /// Create a  new comparer that uses the specified culture and options when comparing string literals.
    /// </summary>
    /// <param name="culture">The culture to use for string literal comparison.</param>
    /// <param name="options">The string comparison options to apply to string literal comparison.</param>
    public SparqlOrderingComparer(CultureInfo culture, CompareOptions options) : base(culture, options)
    {

    }

    /// <summary>
    /// Compares two Nodes.
    /// </summary>
    /// <param name="x">Node.</param>
    /// <param name="y">Node.</param>
    /// <returns></returns>
    public override int Compare(INode x, INode y)
    {
        // Nulls are less than everything
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // If the Node Types are different use Node ordering
        if (x.NodeType != y.NodeType) return x.CompareTo(y);

        if (x.NodeType == NodeType.Literal)
        {
            try
            {
                // Do they have supported Data Types?
                string xtype, ytype;
                try
                {
                    xtype = XmlSpecsHelper.GetSupportedDataType(x);
                    ytype = XmlSpecsHelper.GetSupportedDataType(y);
                }
                catch (RdfException)
                {
                    // Can't determine a Data Type for one/both of the Nodes so use Node ordering
                    return x.CompareTo(y);
                }

                if (xtype.Equals(string.Empty) || ytype.Equals(string.Empty))
                {
                    // One/both has an unknown type
                    if (x.Equals(y))
                    {
                        // If RDF Term equality returns true then we return that they are equal
                        return 0;
                    }

                    // If RDF Term equality returns false then we fall back to Node Ordering
                    return x.CompareTo(y);
                }

                // Both have known types

                if (SparqlSpecsHelper.IsStringDatatype(xtype))

                {
                    if (SparqlSpecsHelper.IsStringDatatype(ytype))
                    {
                        // Compare literal values using the current culture ordering and string comparison options
                        return Culture.CompareInfo.Compare(((ILiteralNode) x).Value, ((ILiteralNode) y).Value,
                            Options);
                    }

                    return -1;
                }

                if (SparqlSpecsHelper.IsStringDatatype(ytype))
                {
                    return 1;
                }

                SparqlNumericType xnumtype = NumericTypesHelper.GetNumericTypeFromDataTypeUri(xtype);
                SparqlNumericType ynumtype = NumericTypesHelper.GetNumericTypeFromDataTypeUri(ytype);
                var numtype = (SparqlNumericType) Math.Max((int) xnumtype, (int) ynumtype);
                if (numtype != SparqlNumericType.NaN)
                {
                    if (xnumtype == SparqlNumericType.NaN)
                    {
                        return 1;
                    }

                    if (ynumtype == SparqlNumericType.NaN)
                    {
                        return -1;
                    }

                    // Both are Numeric so use Numeric ordering
                    try
                    {
                        return NumericCompare(x, y, numtype);
                    }
                    catch (FormatException)
                    {
                        // If this errors try RDF Term equality since that might still give equality, otherwise fall back to Node Ordering
                        return x.Equals(y) ? 0 : x.CompareTo(y);
                    }
                    catch (RdfQueryException)
                    {
                        // If this errors try RDF Term equality since that might still give equality, otherwise fall back to Node Ordering
                        return x.Equals(y) ? 0 : x.CompareTo(y);
                    }
                }

                if (xtype.Equals(ytype))
                {
                    switch (xtype)
                    {
                        case XmlSpecsHelper.XmlSchemaDataTypeDate:
                            return DateCompare(x, y);
                        case XmlSpecsHelper.XmlSchemaDataTypeDateTime:
                            return DateTimeCompare(x, y);
                        default:
                            // Use node ordering
                            return x.CompareTo(y);
                    }
                }

                var commonType = XmlSpecsHelper.GetCompatibleSupportedDataType(xtype, ytype, true);
                if (commonType.Equals(string.Empty))
                {
                    // Use Node ordering
                    return x.CompareTo(y);
                }

                switch (commonType)
                {
                    case XmlSpecsHelper.XmlSchemaDataTypeDate:
                        return DateCompare(x, y);
                    case XmlSpecsHelper.XmlSchemaDataTypeDateTime:
                        return DateTimeCompare(x, y);
                    default:
                        // Use Node ordering
                        return x.CompareTo(y);
                }
            }
            catch
            {
                // If error then can't determine ordering so fall back to node ordering
                return x.CompareTo(y);
            }
        }

        if (x.NodeType == NodeType.Triple)
        {
            Triple xt = (x as ITripleNode)?.Triple, yt = (y as ITripleNode)?.Triple;
            if (xt == null || yt == null) 
            {
                return x.CompareTo(y);
            }
            // Comparing two triple nodes
            var cmp = Compare(xt.Subject, yt.Subject);
            if (cmp == 0)
            {
                cmp = Compare(xt.Predicate, yt.Predicate);
                if (cmp == 0)
                {
                    cmp = Compare(xt.Object, yt.Object);
                }
            }

            return cmp;
        }

        // If not Literals use Node ordering
        return x.CompareTo(y);
    }

    /// <summary>
    /// Compares two Nodes.
    /// </summary>
    /// <param name="x">Node.</param>
    /// <param name="y">Node.</param>
    /// <returns></returns>
    public override int Compare(IValuedNode x, IValuedNode y)
    {
        // Nulls are less than everything
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // If the Node Types are different use Node ordering
        if (x.NodeType != y.NodeType) return x.CompareTo(y);

        if (x.NodeType == NodeType.Literal)
        {
            try
            {
                // Do they have supported Data Types?
                string xtype, ytype;
                try
                {
                    xtype = XmlSpecsHelper.GetSupportedDataType(x);
                    ytype = XmlSpecsHelper.GetSupportedDataType(y);
                }
                catch (RdfException)
                {
                    // Can't determine a Data Type for one/both of the Nodes so use Node ordering
                    return x.CompareTo(y);
                }

                if (xtype.Equals(string.Empty) || ytype.Equals(string.Empty))
                {
                    // One/both has an unknown type
                    if (x.Equals(y))
                    {
                        // If RDF Term equality returns true then we return that they are equal
                        return 0;
                    }
                    else
                    {
                        // If RDF Term equality returns false then we fall back to Node Ordering
                        return x.CompareTo(y);
                    }
                }
                else
                {
                    // Both have known types
                    SparqlNumericType xnumtype = x.NumericType;
                    SparqlNumericType ynumtype = y.NumericType;
                    var numtype = (SparqlNumericType)Math.Max((int)xnumtype, (int)ynumtype);
                    if (numtype != SparqlNumericType.NaN)
                    {
                        if (xnumtype == SparqlNumericType.NaN)
                        {
                            return 1;
                        }
                        else if (ynumtype == SparqlNumericType.NaN)
                        {
                            return -1;
                        }

                        // Both are Numeric so use Numeric ordering
                        try
                        {
                            return NumericCompare(x, y, numtype);
                        }
                        catch (FormatException)
                        {
                            if (x.Equals(y)) return 0;
                            // Otherwise fall back to Node Ordering
                            return x.CompareTo(y);
                        }
                        catch (RdfQueryException)
                        {
                            // If this errors try RDF Term equality since that might still give equality
                            if (x.Equals(y)) return 0;
                            // Otherwise fall back to Node Ordering
                            return x.CompareTo(y);
                        }
                    }
                    else if (xtype.Equals(ytype))
                    {
                        switch (xtype)
                        {
                            case XmlSpecsHelper.XmlSchemaDataTypeDate:
                                return DateCompare(x, y);
                            case XmlSpecsHelper.XmlSchemaDataTypeDateTime:
                                return DateTimeCompare(x, y);
                            case XmlSpecsHelper.XmlSchemaDataTypeString:
                                // Both Strings so use Lexical string ordering
                                return ((ILiteralNode)x).Value.CompareTo(((ILiteralNode)y).Value);
                            default:
                                // Use node ordering
                                return x.CompareTo(y);
                        }
                    }
                    else
                    {
                        var commontype = XmlSpecsHelper.GetCompatibleSupportedDataType(xtype, ytype, true);
                        if (commontype.Equals(string.Empty))
                        {
                            // Use Node ordering
                            return x.CompareTo(y);
                        }
                        else
                        {
                            switch (commontype)
                            {
                                case XmlSpecsHelper.XmlSchemaDataTypeDate:
                                    return DateCompare(x, y);
                                case XmlSpecsHelper.XmlSchemaDataTypeDateTime:
                                    return DateTimeCompare(x, y);
                                default:
                                    // Use Node ordering
                                    return x.CompareTo(y);
                            }
                        }
                    }
                }
            }
            catch
            {
                // If error then can't determine ordering so fall back to node ordering
                return x.CompareTo(y);
            }
        }

        if (x.NodeType == NodeType.Triple)
        {
            Triple xt = (x as ITripleNode)?.Triple, yt = (y as ITripleNode)?.Triple;
            if (xt == null || yt == null)
            {
                return x.CompareTo(y);
            }
            // Comparing two triple nodes
            var cmp = Compare(xt.Subject, yt.Subject);
            if (cmp == 0)
            {
                cmp = Compare(xt.Predicate, yt.Predicate);
                if (cmp == 0)
                {
                    cmp = Compare(xt.Object, yt.Object);
                }
            }

            return cmp;
        }

        // If not Literals use Node ordering
        return x.CompareTo(y);
    }

    /// <summary>
    /// Compares two Date Times for Date Time ordering.
    /// </summary>
    /// <param name="x">Node.</param>
    /// <param name="y">Node.</param>
    /// <returns></returns>
    protected override int DateTimeCompare(IValuedNode x, IValuedNode y)
    {
        if (x == null || y == null) throw new RdfQueryException("Cannot evaluate date time comparison when one or both arguments are Null");
        try
        {
            DateTime c = x.AsDateTime();
            DateTime d = y.AsDateTime();

            switch (c.Kind)
            {
                case DateTimeKind.Unspecified:
                    // Sort unspecified lower than Local/UTC date
                    if (d.Kind != DateTimeKind.Unspecified) return -1;
                    break;
                case DateTimeKind.Local:
                    // Sort Local higher than Unspecified
                    if (d.Kind == DateTimeKind.Unspecified) return 1;
                    c = c.ToUniversalTime();
                    if (d.Kind == DateTimeKind.Local) d = d.ToUniversalTime();
                    break;
                default:
                    // Sort UTC higher than Unspecified
                    if (d.Kind == DateTimeKind.Unspecified) return 1;
                    if (d.Kind == DateTimeKind.Local) d = d.ToUniversalTime();
                    break;
            }

            // Compare on unspecified/UTC form as appropriate
            return c.CompareTo(d);
        }
        catch (FormatException)
        {
            throw new RdfQueryException("Cannot evaluate date time comparison since one of the arguments does not have a valid lexical value for a Date");
        }
    }
}