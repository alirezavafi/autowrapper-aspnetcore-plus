// https://github.com/ThiagoBarradas/jsonmasking/blob/master/JsonMasking/JsonMasking.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoWrapper.Helpers
{
    public static class JsonMasking
    {
        public static string MaskFields(this string json, string[] blacklist, string mask)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            if (blacklist == null)
            {
                throw new ArgumentNullException(nameof(blacklist));
            }

            if (blacklist.Any() == false)
            {
                return json;
            }

            var jsonObject = JsonConvert.DeserializeObject(json);
            if (jsonObject is JArray jArray)
            {
                foreach (var jToken in jArray)
                {
                    MaskFieldsFromJToken(jToken, blacklist, mask);
                }
            }
            else if (jsonObject is JObject jObject)
            {
                MaskFieldsFromJToken(jObject, blacklist, mask);
            }

            return jsonObject.ToString();
        }

        private static void MaskFieldsFromJToken(JToken token, string[] blacklist, string mask)
        {
            JContainer container = token as JContainer;
            if (container == null)
            {
                return; // abort recursive
            }

            List<JToken> removeList = new List<JToken>();
            foreach (JToken jtoken in container.Children())
            {
                if (jtoken is JProperty prop)
                {
                    var matching = blacklist.Any(item =>
                    {
                        return
                            Regex.IsMatch(prop.Path, WildCardToRegular(item),
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    });

                    if (matching)
                    {
                        removeList.Add(jtoken);
                    }
                }

                // call recursive 
                MaskFieldsFromJToken(jtoken, blacklist, mask);
            }

            // replace 
            foreach (JToken el in removeList)
            {
                var prop = (JProperty) el;
                prop.Value = mask;
            }
        }

        private static string WildCardToRegular(string value)
        {
            return "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
        }
    }
}