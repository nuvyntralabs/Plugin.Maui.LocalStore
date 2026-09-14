using Realms;

namespace Plugin.Maui.LocalStore;

public partial class RealmStoreRow : IRealmObject
{
    [PrimaryKey]
    public string Key { get; set; } = "";

    public string Collection { get; set; } = "";

    public string Payload { get; set; } = "";
}
