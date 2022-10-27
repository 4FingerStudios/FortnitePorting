﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using SkiaSharp;

namespace FortnitePorting.Exports;

public static class ExportHelpers
{
    public static void CharacterParts(IEnumerable<UObject> inputParts, List<ExportPart> exportParts)
    {
        var headMorphType = ECustomHatType.None;
        var headMorphNames = new Dictionary<ECustomHatType, string>();
        foreach (var part in inputParts)
        {
            var skeletalMesh = part.Get<USkeletalMesh?>("SkeletalMesh");
            if (skeletalMesh is null) continue;

            if (!skeletalMesh.TryConvert(out var convertedMesh)) continue;
            if (convertedMesh.LODs.Count <= 0) continue;

            var exportPart = new ExportPart();
            exportPart.MeshPath = skeletalMesh.GetPathName();
            Save(skeletalMesh);

            var characterPartType = part.GetOrDefault<EFortCustomPartType>("CharacterPartType");
            exportPart.Part = characterPartType.ToString();

            if (part.TryGetValue<UObject>(out var additionalData, "AdditionalData"))
            {
                var socketName = additionalData.GetOrDefault<FName?>("AttachSocketName");
                exportPart.SocketName = socketName?.PlainText ?? null;
                
                if (additionalData.TryGetValue(out FName hatType, "HatType"))
                {
                    Enum.TryParse(hatType.PlainText.Replace("ECustomHatType::ECustomHatType_", string.Empty), out headMorphType);
                }

                if (additionalData.ExportType.Equals("CustomCharacterHeadData"))
                {
                    foreach (var type in Enum.GetValues<ECustomHatType>())
                    {
                        if (additionalData.TryGetValue(out FName[] morphNames, type + "MorphTargets"))
                        {
                            headMorphNames[type] = morphNames[0].PlainText;
                        }
                    }

                }
            }

            var sections = convertedMesh.LODs[0].Sections.Value;
            for (var idx = 0; idx < sections.Length; idx++)
            {
                var section = sections[idx];
                if (section.Material is null) continue;


                if (!section.Material.TryLoad(out var material)) continue;

                var exportMaterial = new ExportMaterial
                {
                    MaterialName = material.Name,
                    SlotIndex = idx
                };

                if (material is UMaterialInstanceConstant materialInstance)
                {
                    var (textures, scalars, vectors) = MaterialParameters(materialInstance);
                    exportMaterial.Textures = textures;
                    exportMaterial.Scalars = scalars;
                    exportMaterial.Vectors = vectors;
                }

                exportMaterial.Hash = material.GetPathName().GetHashCode();
                exportPart.Materials.Add(exportMaterial);

            }

            if (part.TryGetValue(out FStructFallback[] materialOverrides, "MaterialOverrides"))
            {
                OverrideMaterials(materialOverrides, ref exportPart);
            }
            
            exportParts.Add(exportPart);
        }

        if (headMorphType != ECustomHatType.None && headMorphNames.ContainsKey(headMorphType))
        {
            var morphName = headMorphNames[headMorphType];
            exportParts.First(x => x.Part == "Head").MorphName = morphName;
        }
    }

    public static void Weapon(UObject weaponDefinition, List<ExportPart> exportParts)
    {
        // TODO MATERIAL STYLES
        if (weaponDefinition.TryGetValue(out UBlueprintGeneratedClass blueprint, "WeaponActorClass"))
        {
            var defaultObject = blueprint.ClassDefaultObject.Load();
            if (defaultObject.TryGetValue(out UObject weaponMeshData, "WeaponMesh"))
            {
                Mesh(weaponMeshData.GetOrDefault<USkeletalMesh>("SkeletalMesh"), exportParts);
            }

            if (defaultObject.TryGetValue(out UObject leftWeaponMeshData, "LeftHandWeaponMesh"))
            {
                Mesh(leftWeaponMeshData.GetOrDefault<USkeletalMesh>("SkeletalMesh"), exportParts);
            }

            if (exportParts.Count > 0) // successfully exported mesh
                return;
        }

        weaponDefinition.TryGetValue(out USkeletalMesh? skeletalMesh, "PickupSkeletalMesh");
        var hasWeaponOverride = weaponDefinition.TryGetValue(out skeletalMesh, "WeaponMeshOverride");
        Mesh(skeletalMesh, exportParts);

        weaponDefinition.TryGetValue(out USkeletalMesh offHandMesh, "WeaponMeshOffhandOverride");
        Mesh(offHandMesh, exportParts);

        if (!hasWeaponOverride)
        {
            weaponDefinition.TryGetValue(out UStaticMesh? staticMesh, "PickupStaticMesh");
            Mesh(staticMesh, exportParts);
        }
    }

    public static int Mesh(USkeletalMesh? skeletalMesh, List<ExportPart> exportParts)
    {
        if (skeletalMesh is null) return -1;
        if (!skeletalMesh.TryConvert(out var convertedMesh)) return -1;
        if (convertedMesh.LODs.Count <= 0) return -1;

        var exportPart = new ExportPart();
        exportPart.MeshPath = skeletalMesh.GetPathName();
        Save(skeletalMesh);

        var sections = convertedMesh.LODs[0].Sections.Value;
        for (var idx = 0; idx < sections.Length; idx++)
        {
            var section = sections[idx];
            if (section.Material is null) continue;
            
            if (!section.Material.TryLoad(out var material)) continue;

            var exportMaterial = new ExportMaterial
            {
                MaterialName = material.Name,
                SlotIndex = idx
            };

            if (material is UMaterialInstanceConstant materialInstance)
            {
                var (textures, scalars, vectors) = MaterialParameters(materialInstance);
                exportMaterial.Textures = textures;
                exportMaterial.Scalars = scalars;
                exportMaterial.Vectors = vectors;
            }

            exportMaterial.Hash = material.GetPathName().GetHashCode();
            exportPart.Materials.Add(exportMaterial);
        }

        exportParts.Add(exportPart);
        return exportParts.Count - 1;
    }

    public static int Mesh(UStaticMesh? staticMesh, List<ExportPart> exportParts)
    {
        if (staticMesh is null) return -1;
        if (!staticMesh.TryConvert(out var convertedMesh)) return -1;
        if (convertedMesh.LODs.Count <= 0) return -1;

        var exportPart = new ExportPart();
        exportPart.MeshPath = staticMesh.GetPathName();
        Save(staticMesh);

        var sections = convertedMesh.LODs[0].Sections.Value;
        for (var idx = 0; idx < sections.Length; idx++)
        {
            var section = sections[idx];
            if (section.Material is null) continue;


            if (!section.Material.TryLoad(out var material)) continue;

            var exportMaterial = new ExportMaterial
            {
                MaterialName = material.Name,
                SlotIndex = idx
            };

            if (material is UMaterialInstanceConstant materialInstance)
            {
                var (textures, scalars, vectors) = MaterialParameters(materialInstance);
                exportMaterial.Textures = textures;
                exportMaterial.Scalars = scalars;
                exportMaterial.Vectors = vectors;
            }

            exportMaterial.Hash = material.GetPathName().GetHashCode();
            exportPart.Materials.Add(exportMaterial);
        }

        exportParts.Add(exportPart);
        return exportParts.Count - 1;
    }

    public static void OverrideMaterials(FStructFallback[] overrides, ref ExportPart exportPart)
    {
        foreach (var materialOverride in overrides)
        {
            var overrideMaterial = materialOverride.Get<FSoftObjectPath>("OverrideMaterial");
            if (!overrideMaterial.TryLoad(out var material)) continue;

            var exportMaterial = new ExportMaterial
            {
                MaterialName = material.Name,
                SlotIndex = materialOverride.Get<int>("MaterialOverrideIndex"),
                MaterialNameToSwap = materialOverride.GetOrDefault<FSoftObjectPath>("MaterialToSwap").AssetPathName.PlainText.SubstringAfterLast(".")
            };

            if (material is UMaterialInstanceConstant materialInstance)
            {
                var (textures, scalars, vectors) = MaterialParameters(materialInstance);
                exportMaterial.Textures = textures;
                exportMaterial.Scalars = scalars;
                exportMaterial.Vectors = vectors;
            }
            
            exportMaterial.Hash = material.GetPathName().GetHashCode();
            for (var idx = 0; idx < exportPart.Materials.Count; idx++)
            {
                if (exportMaterial.SlotIndex >= exportPart.Materials.Count) continue;
                if (exportPart.Materials[exportMaterial.SlotIndex].Hash ==
                    exportPart.Materials[idx].Hash)
                {
                    exportPart.OverrideMaterials.Add(exportMaterial with { SlotIndex = idx });
                }
            }
        }
    }
    
    public static void OverrideMaterials(FStructFallback[] overrides, List<ExportMaterial> exportMaterials)
    {
        foreach (var materialOverride in overrides)
        {
            var overrideMaterial = materialOverride.Get<FSoftObjectPath>("OverrideMaterial");
            if (!overrideMaterial.TryLoad(out var material)) continue;

            var exportMaterial = new ExportMaterial
            {
                MaterialName = material.Name,
                SlotIndex = materialOverride.Get<int>("MaterialOverrideIndex"),
                MaterialNameToSwap = materialOverride.GetOrDefault<FSoftObjectPath>("MaterialToSwap").AssetPathName.PlainText.SubstringAfterLast(".")
            };

            if (material is UMaterialInstanceConstant materialInstance)
            {
                var (textures, scalars, vectors) = MaterialParameters(materialInstance);
                exportMaterial.Textures = textures;
                exportMaterial.Scalars = scalars;
                exportMaterial.Vectors = vectors;
            }
            
            exportMaterial.Hash = material.GetPathName().GetHashCode();
            exportMaterials.Add(exportMaterial);
        }
    }

    public static (List<TextureParameter>, List<ScalarParameter>, List<VectorParameter>) MaterialParameters(UMaterialInstanceConstant materialInstance)
    {
        var textures = new List<TextureParameter>();
        foreach (var parameter in materialInstance.TextureParameterValues)
        {
            if (!parameter.ParameterValue.TryLoad(out UTexture2D texture)) continue;
            textures.Add(new TextureParameter(parameter.ParameterInfo.Name.PlainText, texture.GetPathName()));
            Save(texture);
        }

        var scalars = new List<ScalarParameter>();
        foreach (var parameter in materialInstance.ScalarParameterValues)
        {
            scalars.Add(new ScalarParameter(parameter.ParameterInfo.Name.PlainText, parameter.ParameterValue));
        }

        var vectors = new List<VectorParameter>();
        foreach (var parameter in materialInstance.VectorParameterValues)
        {
            if (parameter.ParameterValue is null) continue;
            vectors.Add(new VectorParameter(parameter.ParameterInfo.Name.PlainText, parameter.ParameterValue.Value));
        }

        if (materialInstance.Parent is UMaterialInstanceConstant { Parent: UMaterialInstanceConstant } materialParent)
        {
            var (parentTextures, parentScalars, parentVectors) = MaterialParameters(materialParent);
            foreach (var parentTexture in parentTextures)
            {
                if (textures.Any(x => x.Name.Equals(parentTexture.Name))) continue;
                textures.Add(parentTexture);
            }

            foreach (var parentScalar in parentScalars)
            {
                if (scalars.Any(x => x.Name.Equals(parentScalar.Name))) continue;
                scalars.Add(parentScalar);
            }

            foreach (var parentVector in parentVectors)
            {
                if (vectors.Any(x => x.Name.Equals(parentVector.Name))) continue;
                vectors.Add(parentVector);
            }
        }
        return (textures, scalars, vectors);
    }

    public static readonly List<Task> Tasks = new();
    private static readonly ExporterOptions ExportOptions = new()
    {
        Platform = ETexturePlatform.DesktopMobile,
        LodFormat = ELodFormat.FirstLod,
        MeshFormat = EMeshFormat.ActorX,
        TextureFormat = ETextureFormat.Png,
        ExportMorphTargets = true,
        SocketFormat = ESocketFormat.Bone
    };

    public static void Save(UObject obj)
    {
        Tasks.Add(Task.Run(() =>
        {
            try
            {
                switch (obj)
                {
                    case USkeletalMesh skeletalMesh:
                    {
                        var path = GetExportPath(obj, "psk", "_LOD0");
                        if (File.Exists(path)) return;

                        var exporter = new MeshExporter(skeletalMesh, ExportOptions, false);
                        exporter.TryWriteToDir(App.AssetsFolder, out _);
                        break;
                    }

                    case UStaticMesh staticMesh:
                    {
                        var path = GetExportPath(obj, "pskx", "_LOD0");
                        if (File.Exists(path)) return;

                        var exporter = new MeshExporter(staticMesh, ExportOptions, false);
                        exporter.TryWriteToDir(App.AssetsFolder, out _);
                        break;
                    }
                    case UTexture2D texture:
                    {
                        var path = GetExportPath(obj, "png");
                        if (File.Exists(path)) return;
                        Directory.CreateDirectory(path.Replace('\\', '/').SubstringBeforeLast('/'));

                        using var bitmap = texture.Decode(texture.GetFirstMip());
                        using var data = bitmap?.Encode(SKEncodedImageFormat.Png, 100);

                        if (data is null) return;
                        File.WriteAllBytes(path, data.ToArray());
                        break;
                    }
                }
            }
            catch (IOException) { }
        }));
    }

    private static string GetExportPath(UObject obj, string ext, string extra = "")
    {
        var path = obj.Owner.Name;
        path = path.SubstringBeforeLast('.');
        if (path.StartsWith("/")) path = path[1..];

        var finalPath = Path.Combine(App.AssetsFolder.FullName, path) + $"{extra}.{ext.ToLower()}";
        return finalPath;
    }
}