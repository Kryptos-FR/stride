// Copyright (c) Xenko contributors (https://xenko.com)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.IO;
using System.Resources;

namespace Xenko.Core.Translation.Resources
{
    public class GettextResourceSet : ResourceSet
    {
        public GettextResourceSet(string fileName)
            : base(fileName)
        {
        }

        public GettextResourceSet(Stream stream)
            : base(stream)
        {
        }

        public GettextResourceSet(IResourceReader reader)
            : base(reader)
        {
        }

        public override string GetString(string text)
        {
            throw new NotImplementedException();
        }

        public string GetPluralString(string text, string textPlural, long count)
        {
            throw new NotImplementedException();
        }

        public string GetParticularString(string context, string text)
        {
            throw new NotImplementedException();
        }

        public string GetParticularPluralString(string context, string text, string textPlural, long count)
        {
            throw new NotImplementedException();
        }
    }
}
