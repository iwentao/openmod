﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenMod.Common.Helpers
{
    /// <summary>
    /// Extension methods for <see cref="System.Reflection.Assembly"/>.
    /// </summary>
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Safely returns the set of loadable types from an assembly.
        /// </summary>
        /// <param name="assembly">The <see cref="System.Reflection.Assembly"/> from which to load types.</param>
        /// <returns>
        /// The set of types from the <paramref name="assembly" />, or the subset
        /// of types that could be loaded if there was any error.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if <paramref name="assembly" /> is <see langword="null" />.
        /// </exception>
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            // Algorithm from StackOverflow answer here:
            // https://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            try
            {
                // Its important that it is ToList() here or the exception may not get caught
                return assembly.DefinedTypes.Select(t => t.AsType()).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }
    }
}