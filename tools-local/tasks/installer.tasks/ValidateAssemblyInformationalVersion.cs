// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Validates that the AssemblyInformationalVersion matches the product version
    /// </summary>
    public class ValidateAssemblyInformationalVersion : Task
    {
        [Required]
        public string AssemblyPath { get; set; }

        [Required]
        public string ProductVersion { get; set; }

        public override bool Execute()
        {
            using FileStream stream = File.OpenRead(AssemblyPath);
            MetadataReader metadataReader = new PEReader(stream).GetMetadataReader();
            AssemblyDefinition assemblyDefinition = metadataReader.GetAssemblyDefinition();

            CustomAttribute customAttribute = default;
            bool foundAttribute = false;
            foreach (CustomAttributeHandle customAttributeHandle in assemblyDefinition.GetCustomAttributes())
            {
                if (customAttributeHandle.IsAssemblyInformationalVersionAttribute(metadataReader, out customAttribute))
                {
                    foundAttribute = true;
                    break;
                }
            }

            string versionString = null;
            if (foundAttribute)
            {
                CustomAttributeValue<string> attributeValue = customAttribute.DecodeValue(new CustomAttributeTypeProvider());
                if (attributeValue.FixedArguments.Length > 0)
                {
                    versionString = (string)attributeValue.FixedArguments[0].Value;
                    if (versionString != null)
                    {
                        int plusIndex = versionString.IndexOf('+');
                        if (plusIndex != -1)
                        {
                            versionString = versionString.Substring(0, plusIndex);
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(versionString))
            {
                Log.LogError($"Couldn't find a valid version on AssemblyInformationalVersionAttribute in: {AssemblyPath}");
                return false;
            }

            if (!versionString.Equals(ProductVersion, StringComparison.Ordinal))
            {
                Log.LogError($"AssemblyInformationalVersion {versionString} doesn't match expected version {ProductVersion} in: {AssemblyPath}");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, $"AssemblyInformationalVersion {versionString} matched expected version {ProductVersion} in: {AssemblyPath}");

            return true;
        }
    }

    internal class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public string GetSystemType()
        {
            return "[System.Runtime]System.Type";
        }

        public bool IsSystemType(string type) => false;

        public string GetTypeFromSerializedName(string name)
        {
            return name;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.String => "string",
                _ => throw new ArgumentOutOfRangeException(nameof(typeCode)),
            };
        }

        public string GetSZArrayType(string elementType) => null;
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => default;
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0) => null;
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind = 0) => null;
    }

    internal static class MetadataReaderExtensions
    {
        private static bool Equals(this StringHandle handle, string other, MetadataReader reader) =>
            reader.GetString(handle).Equals(other, StringComparison.Ordinal);

        private static bool TypeMatchesNameAndNamespace(this EntityHandle handle, string @namespace, string name, MetadataReader reader)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    TypeDefinition td = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return !td.Namespace.IsNil && td.Namespace.Equals(@namespace, reader) && td.Name.Equals(name, reader);
                case HandleKind.TypeReference:
                    TypeReference tr = reader.GetTypeReference((TypeReferenceHandle)handle);
                    return !tr.Namespace.IsNil && tr.Namespace.Equals(@namespace, reader) && tr.Name.Equals(name, reader);
                default:
                    return false;
            }
        }

        public static bool IsAssemblyInformationalVersionAttribute(this CustomAttributeHandle attributeHandle, MetadataReader reader, out CustomAttribute attribute)
        {
            const string @namespace = "System.Reflection";
            const string name = "AssemblyInformationalVersionAttribute";
            attribute = reader.GetCustomAttribute(attributeHandle);
            EntityHandle ctorHandle = attribute.Constructor;
            switch (ctorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    return reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent.TypeMatchesNameAndNamespace(@namespace, name, reader);
                case HandleKind.MethodDefinition:
                    EntityHandle handle = reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle).GetDeclaringType();
                    return handle.TypeMatchesNameAndNamespace(@namespace, name, reader);
                default:
                    return false;
            }
        }
    }
}
