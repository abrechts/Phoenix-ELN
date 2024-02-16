using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblSeparators
{
    public string GUID { get; set; } = null!;

    public string ProtocolItemID { get; set; } = null!;

    public short ElementType { get; set; }

    public short DisplayType { get; set; }

    public string? Title { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
