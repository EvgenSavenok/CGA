using System.Globalization;
using System.Numerics;
using Graphics.Core.Objects;

namespace Graphics.Core.Parsers;

public static class TextureParser
{
    public static ListOfMaterials Parse(string filePath)
    {
        var pathToTextures = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        var mtlFile = new ListOfMaterials();
        CustomMaterial? currentMaterial = null;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (tokens[0])
            {
                // Новый материал в файле (какой-то их кастомный)
                case "newmtl":
                    if (tokens.Length < 2)
                        throw new ArgumentException("Material name is missing");

                    currentMaterial = new CustomMaterial { Name = tokens[1] };
                    mtlFile.Materials[tokens[1]] = currentMaterial;
                    break;
                case "Ka": // Фоновый цвет
                case "Kd": // Основной цвет
                case "Ks": // Цвет бликов
                    if (tokens.Length < 4)
                        throw new ArgumentException($"Incorrect color format at line: {line}");

                    var color = new Vector3(
                        float.Parse(tokens[1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[3], CultureInfo.InvariantCulture));

                    if (tokens[0] == "Ka") currentMaterial!.AmbientColor = color;
                    if (tokens[0] == "Kd") currentMaterial!.DiffuseColor = color;
                    if (tokens[0] == "Ks") currentMaterial!.SpecularColor = color;
                    break;
                case "Ns": // Коэффициент блеска
                    currentMaterial!.Shininess = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                case "d": // Прозрачность
                    currentMaterial!.Transparency = 1f - float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                case "Tr": // Альтернативный способ указания прозрачности
                    currentMaterial!.Transparency = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                case "Ni": // Показатель преломления
                    currentMaterial!.OpticalDensity = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                case "illum": // Модель освещения (с тенями или без них)
                    currentMaterial!.IlluminationModel = int.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                // Для диффузной карты
                case "map_Kd":
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.DiffuseMap = pathToTextures +
                                                     Path.GetFileName(string.Join("", string.Join("", tokens[1..])));
                    break;
                case "norm":
                case "map_Norm": // карта нормалей (для освещения)
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.NormalMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_bump": // карта рельефа
                    if (currentMaterial != null)
                    {
                        // Если после map_bump присутствует параметр "-bm"
                        // Это дополнительный параметр масштабирования
                        if (tokens.Length >= 4 && tokens[1].ToLowerInvariant() == "-bm")
                        {
                            // tokens[2] - коэффициент, tokens[3] - путь к текстуре
                            currentMaterial.BumpScale = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                            currentMaterial.BumpMap = pathToTextures + Path.GetFileName(string.Join("", tokens[3..]));
                        }
                        else if (tokens.Length >= 2)
                        {
                            // Если нет параметра -bm, просто берем путь
                            currentMaterial.BumpMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                        }
                    }

                    break;
                case "map_mrao": // (metallic - roughness - ambient occlusion)
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.MraoMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_ao": // текстура ambient occlusion
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.AoMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_metallic":
                case "map_refl":
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.MetallicMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_roughness":
                case "map_ns":
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.RoughnessMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_ke":
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.EmissiveMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
                case "map_specular":
                    if (currentMaterial != null && tokens.Length >= 2)
                        currentMaterial.SpecularMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
                    break;
            }
        }

        return mtlFile;
    }
}