using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblSearchIndex
{
    public string ProtocolItemID { get; set; } = null!;

    public string ExperimentID { get; set; } = null!;

    public string Content { get; set; } = null!;

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
