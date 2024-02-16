using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblComments
{
    public string GUID { get; set; } = null!;

    public string ProtocolItemID { get; set; } = null!;

    public string? CommentFlowDoc { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
