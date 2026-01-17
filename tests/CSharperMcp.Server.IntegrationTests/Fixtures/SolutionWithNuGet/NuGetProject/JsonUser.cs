using Newtonsoft.Json.Linq;

namespace NuGetProject;

public class JsonUser
{
    public string SerializeData(Dictionary<string, object> data)
    {
        var jObject = JObject.FromObject(data);
        return jObject.ToString();
    }

    public JObject ParseJson(string json)
    {
        return JObject.Parse(json);
    }

    public void UseJsonMethods()
    {
        // Uses various JObject methods to test symbol resolution
        var obj = new JObject();
        obj.Add("key", "value");
        obj.Remove("key");

        var hasProperty = obj.ContainsKey("key");
        var propertyValue = obj["key"];
    }
}
