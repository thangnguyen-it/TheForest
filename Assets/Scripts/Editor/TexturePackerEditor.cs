using UnityEngine;
using UnityEditor;
using System.IO;

public class TexturePackerEditor : EditorWindow
{
    [MenuItem("Tools/Pack Texture Alpha")]
    public static void PackAlpha()
    {
        if (Selection.objects.Length < 2)
        {
            EditorUtility.DisplayDialog("Lỗi cấu hình", "Sơn vui lòng giữ phím Ctrl và click chọn cả 2 file ảnh: file Diffuse trước, file Opacity sau ở cửa sổ Project nhé!", "OK");
            return;
        }

        Texture2D diffuse = Selection.objects[0] as Texture2D;
        Texture2D opacity = Selection.objects[1] as Texture2D;

        if (diffuse == null || opacity == null)
        {
            EditorUtility.DisplayDialog("Lỗi định dạng", "Hai file bạn chọn phải là định dạng ảnh (Texture2D) mới xử lý được nhé!", "OK");
            return;
        }

        string diffPath = AssetDatabase.GetAssetPath(diffuse);
        string opPath = AssetDatabase.GetAssetPath(opacity);

        TextureImporter diffImporter = AssetImporter.GetAtPath(diffPath) as TextureImporter;
        TextureImporter opImporter = AssetImporter.GetAtPath(opPath) as TextureImporter;

        if (diffImporter != null)
        {
            diffImporter.isReadable = true;
            diffImporter.textureCompression = TextureImporterCompression.Uncompressed;
            diffImporter.SaveAndReimport();
        }

        if (opImporter != null)
        {
            opImporter.isReadable = true;
            opImporter.textureCompression = TextureImporterCompression.Uncompressed;
            opImporter.SaveAndReimport();
        }

        Texture2D packedTexture = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, false);
        Color[] diffPixels = diffuse.GetPixels();
        Color[] opPixels = opacity.GetPixels();

        if (diffPixels.Length != opPixels.Length)
        {
            EditorUtility.DisplayDialog("Lỗi kích thước", "Kích thước của 2 bức ảnh này không khớp nhau, Sơn check lại xem có chọn nhầm ảnh không nha!", "OK");
            return;
        }

        for (int i = 0; i < diffPixels.Length; i++)
        {
            diffPixels[i].a = opPixels[i].r;
        }

        packedTexture.SetPixels(diffPixels);
        packedTexture.Apply();

        byte[] pngBytes = packedTexture.EncodeToPNG();
        string directory = Path.GetDirectoryName(diffPath);
        string newPath = Path.Combine(directory, diffuse.name + "_AlphaPacked.png");

        File.WriteAllBytes(newPath, pngBytes);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Thành công", "Đã gộp kênh Alpha xong xuôi! File mới có sẵn Alpha đã xuất hiện cạnh file gốc nha Sơn!", "OK");
    }
}