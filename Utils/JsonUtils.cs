using LMC.Basic;
using System;
using System.Text.Json;

namespace LMC.Utils
{
    public static class JsonUtils
    {

        public static string GetValueFromJson(string jsonString, string path)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(jsonString))
                {
                    string[] keys = path.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                    JsonElement element = document.RootElement;
                    foreach (var key in keys)
                    {
                        if (key.Contains("["))
                        {
                            var parts = key.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                            var arrayKey = parts[0];
                            var index = int.Parse(parts[1]);

                            if (element.TryGetProperty(arrayKey, out JsonElement arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
                            {
                                element = arrayElement[index];
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            if (element.TryGetProperty(key, out JsonElement nextElement))
                            {
                                element = nextElement;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                    return element.ToString();
                }
            }
            catch (Exception ex) {
                new Logger("JU").Warn("解析json失败：" + ex.Message + "\n" + ex.StackTrace);
                return string.Empty;
            }
        }
    }
}