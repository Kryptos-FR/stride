// Copyright (c) Xenko contributors (https://xenko.com)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Xenko.Core.Translation.Resources
{
    public class GettextResourceManager : ResourceManager
    {
        private readonly ConcurrentDictionary<CultureInfo, GettextResourceSet> _cache = new ConcurrentDictionary<CultureInfo, GettextResourceSet>();

        public GettextResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly, typeof(GettextResourceSet))
        {
        }
        
        public override string GetString(string text)
        {
            return GetString(text, CultureInfo.CurrentUICulture);
        }
        
        public override string GetString(string text, CultureInfo culture)
        {
            foreach (var resourceSet in GetResourceSets(culture))
            {
                var translation = resourceSet.GetString(text);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }

            // fallback
            return text;
        }

        public string GetPluralString(string text, string textPlural, long count)
        {
            foreach (var resourceSet in GetResourceSets(CultureInfo.CurrentUICulture))
            {
                var translation = resourceSet.GetPluralString(text, textPlural, count);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            
            // fallback
            return count > 1 ? textPlural : text;
        }

        public string GetParticularString(string context, string text)
        {
            foreach (var resourceSet in GetResourceSets(CultureInfo.CurrentUICulture))
            {
                var translation = resourceSet.GetParticularString(context, text);
                if (!string.IsNullOrEmpty(translation))
                    return translation;

                // try without context
                translation = resourceSet.GetString(text);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }

            // fallback
            return text;
        }

        public string GetParticularPluralString(string context, string text, string textPlural, long count)
        {
            foreach (var resourceSet in GetResourceSets(CultureInfo.CurrentUICulture))
            {
                var translation = resourceSet.GetParticularPluralString(context, text, textPlural, count);
                if (!string.IsNullOrEmpty(translation))
                    return translation;

                // try without context
                translation = resourceSet.GetPluralString(text, textPlural, count);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            
            // fallback
            return count > 1 ? textPlural : text;
        }

        private IEnumerable<GettextResourceSet> GetResourceSets(CultureInfo culture)
        {

            // TODO
            yield break;
        }
    }
}
