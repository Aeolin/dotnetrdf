/*
// <copyright>
// dotNetRDF is free and open source software licensed under the MIT License
// -------------------------------------------------------------------------
// 
// Copyright (c) 2009-2021 dotNetRDF Project (http://dotnetrdf.org/)
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
using System.Linq;
using System.Text;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Paths;

namespace VDS.RDF.Query.Patterns
{
    /// <summary>
    /// Class for representing property patterns in SPARQL Queries.
    /// </summary>
    public class PropertyPathPattern
        : BaseTriplePattern, IPropertyPathPattern, IComparable<PropertyPathPattern>
    {
        /// <summary>
        /// Creates a new Property Path Pattern.
        /// </summary>
        /// <param name="subj">Subject.</param>
        /// <param name="path">Property Path.</param>
        /// <param name="obj">Object.</param>
        public PropertyPathPattern(PatternItem subj, ISparqlPath path, PatternItem obj)
        {
            Subject = subj;
            Path = path;
            Object = obj;
            Subject.RigorousEvaluation = true;
            Object.RigorousEvaluation = true;

            // Build our list of Variables
            if (Subject.VariableName != null)
            {
                _vars.Add(Subject.VariableName);
            }
            if (Object.VariableName != null)
            {
                if (!_vars.Contains(Object.VariableName)) _vars.Add(Object.VariableName);
            }
            _vars.Sort();
        }

        /// <summary>
        /// Gets the pattern type.
        /// </summary>
        public override TriplePatternType PatternType => TriplePatternType.Path;

        /// <summary>
        /// Gets the Subject of the Property Path.
        /// </summary>
        public PatternItem Subject { get; }

        /// <summary>
        /// Gets the Property Path.
        /// </summary>
        public ISparqlPath Path { get; }

        /// <summary>
        /// Gets the Object of the Property Path.
        /// </summary>
        public PatternItem Object { get; }

        /// <summary>
        /// Gets the enumeration of fixed variables in the pattern i.e. variables that are guaranteed to have a bound value.
        /// </summary>
        public override IEnumerable<string> FixedVariables => _vars;

        /// <summary>
        /// Gets the enumeration of floating variables in the pattern i.e. variables that are not guaranteed to have a bound value.
        /// </summary>
        public override IEnumerable<string> FloatingVariables { get; } = Enumerable.Empty<string>();

        /// <summary>
        /// Evaluates a property path pattern.
        /// </summary>
        /// <param name="context">Evaluation Context.</param>
        public override void Evaluate(SparqlEvaluationContext context)
        {
            // Try and generate an Algebra expression
            // Make sure we don't generate clashing temporary variable IDs over the life of the
            // Evaluation
            var transformContext = new PathTransformContext(Subject, Object);
            if (context["PathTransformID"] != null)
            {
                transformContext.NextID = (int)context["PathTransformID"];
            }
            ISparqlAlgebra algebra = Path.ToAlgebra(transformContext);
            context["PathTransformID"] = transformContext.NextID + 1;

            // Now we can evaluate the resulting algebra
            BaseMultiset initialInput = context.InputMultiset;
            var trimMode = context.TrimTemporaryVariables;
            var rigMode = context.Options.RigorousEvaluation;
            try
            {
                // Must enable rigorous evaluation or we get incorrect interactions between property and non-property path patterns
                context.Options.RigorousEvaluation = true;

                // Note: We may need to preserve Blank Node variables across evaluations
                // which we usually don't do BUT because of the way we translate only part of the path
                // into an algebra at a time and may need to do further nested translate calls we do
                // need to do this here
                context.TrimTemporaryVariables = false;
                BaseMultiset result = context.Evaluate(algebra);//algebra.Evaluate(context);
                // Also note that we don't trim temporary variables here even if we've set the setting back
                // to enabled since a Trim will be done at the end of whatever BGP we are being evaluated in

                // Once we have our results can join then into our input
                context.OutputMultiset = result is NullMultiset ? new NullMultiset() : initialInput.Join(result);

                // If we reach here we've successfully evaluated the simple pattern and can return
                return;
            }
            finally
            {
                context.TrimTemporaryVariables = trimMode;
                context.Options.RigorousEvaluation = rigMode;
            }
        }

        /// <summary>
        /// Gets whether the Pattern accepts all Triple Patterns.
        /// </summary>
        public override bool IsAcceptAll => false;

        /// <summary>
        /// Returns false a property path may always contain implicit blank variables.
        /// </summary>
        public override bool HasNoBlankVariables => false;

        /// <summary>
        /// Compares a property path pattern to another.
        /// </summary>
        /// <param name="other">Pattern.</param>
        /// <returns></returns>
        public int CompareTo(PropertyPathPattern other)
        {
            return CompareTo((IPropertyPathPattern)other);
        }

        /// <summary>
        /// Compares a property path pattern to another.
        /// </summary>
        /// <param name="other">Pattern.</param>
        /// <returns></returns>
        public int CompareTo(IPropertyPathPattern other)
        {
            return base.CompareTo(other);
        }

        /// <summary>
        /// Gets the String representation of the Pattern.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var output = new StringBuilder();
            output.Append(Subject.ToString());
            output.Append(' ');
            output.Append(Path.ToString());
            output.Append(' ');
            output.Append(Object.ToString());
            return output.ToString();
        }
    }
}
