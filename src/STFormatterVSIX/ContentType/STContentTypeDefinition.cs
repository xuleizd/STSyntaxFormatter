using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace STFormatterVSIX.ContentType
{
    /// <summary>
    /// Registers the StructuredText content type for .TcPOU files so that
    /// Visual Studio recognises them as code documents.
    /// </summary>
    public class STContentTypeDefinition
    {
        public const string STContentType = "StructuredText";

        [Export(typeof(ContentTypeDefinition))]
        [Name(STContentType)]
        [BaseDefinition("code")]
        public ContentTypeDefinition STContentTypeDef { get; set; }

        [Export(typeof(FileExtensionToContentTypeDefinition))]
        [ContentType(STContentType)]
        [FileExtension(".TcPOU")]
        public FileExtensionToContentTypeDefinition TcPouExtension { get; set; }
    }
}
