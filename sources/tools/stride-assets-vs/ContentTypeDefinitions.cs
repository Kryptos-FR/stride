using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

#pragma warning disable CS0649 // MEF fields are assigned by the composition container

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Defines the "stride-asset" content type and maps all Stride asset file extensions to it.
    /// </summary>
    internal static class ContentTypeDefinitions
    {
        [Export]
        [Name("stride-asset")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition? StrideAssetContentType;

        // --- File extension mappings ---

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdscene")]
        internal static FileExtensionToContentTypeDefinition? SdSceneExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdprefab")]
        internal static FileExtensionToContentTypeDefinition? SdPrefabExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdmat")]
        internal static FileExtensionToContentTypeDefinition? SdMatExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdtex")]
        internal static FileExtensionToContentTypeDefinition? SdTexExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdm3d")]
        internal static FileExtensionToContentTypeDefinition? SdM3dExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdanim")]
        internal static FileExtensionToContentTypeDefinition? SdAnimExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdskel")]
        internal static FileExtensionToContentTypeDefinition? SdSkelExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdgfxcomp")]
        internal static FileExtensionToContentTypeDefinition? SdGfxCompExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdgamesettings")]
        internal static FileExtensionToContentTypeDefinition? SdGameSettingsExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdsheet")]
        internal static FileExtensionToContentTypeDefinition? SdSheetExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdfnt")]
        internal static FileExtensionToContentTypeDefinition? SdFntExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdpkg")]
        internal static FileExtensionToContentTypeDefinition? SdPkgExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdsky")]
        internal static FileExtensionToContentTypeDefinition? SdSkyExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdsnd")]
        internal static FileExtensionToContentTypeDefinition? SdSndExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdvid")]
        internal static FileExtensionToContentTypeDefinition? SdVidExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdrendertex")]
        internal static FileExtensionToContentTypeDefinition? SdRenderTexExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sduipage")]
        internal static FileExtensionToContentTypeDefinition? SdUiPageExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sduilib")]
        internal static FileExtensionToContentTypeDefinition? SdUiLibExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdnavmesh")]
        internal static FileExtensionToContentTypeDefinition? SdNavMeshExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdhmap")]
        internal static FileExtensionToContentTypeDefinition? SdHmapExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdphy")]
        internal static FileExtensionToContentTypeDefinition? SdPhyExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdpcfnt")]
        internal static FileExtensionToContentTypeDefinition? SdPcFntExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdvs")]
        internal static FileExtensionToContentTypeDefinition? SdVsExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdeffectlog")]
        internal static FileExtensionToContentTypeDefinition? SdEffectLogExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdhull")]
        internal static FileExtensionToContentTypeDefinition? SdHullExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdpromodel")]
        internal static FileExtensionToContentTypeDefinition? SdProModelExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdprefabmodel")]
        internal static FileExtensionToContentTypeDefinition? SdPrefabModelExtension;

        [Export]
        [ContentType("stride-asset")]
        [FileExtension(".sdtpl")]
        internal static FileExtensionToContentTypeDefinition? SdTplExtension;
    }
}
