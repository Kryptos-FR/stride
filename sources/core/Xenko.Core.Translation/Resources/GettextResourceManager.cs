// Copyright (c) Xenko contributors (https://xenko.com)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Xenko.Core.Translation.Resources
{
    public class GettextResourceManager : ResourceManager
    {
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
            foreach (var set in GetResourceSets(culture))
            {
                var translation = set.GetString(text);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }

            // fallback
            return text;
        }

        public string GetPluralString(string text, string textPlural, long count)
        {
            foreach (var set in GetResourceSets(CultureInfo.CurrentUICulture))
            {
                var translation = set.GetPluralString(text, textPlural, count);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }
            
            // fallback
            return count > 1 ? textPlural : text;
        }

        public string GetParticularString(string context, string text)
        {
            foreach (var set in GetResourceSets(CultureInfo.CurrentUICulture))
            {
                var translation = set.GetParticularString(context, text);
                if (!string.IsNullOrEmpty(translation))
                    return translation;
            }

            // fallback
            return text;
        }

        public string GetParticularPluralString(string context, string text, string textPlural, long count)
        {
            throw new NotImplementedException();

            return text;
        }

        private IEnumerable<GettextResourceSet> GetResourceSets(CultureInfo culture)
        {
            yield break;
        }
    }
}
