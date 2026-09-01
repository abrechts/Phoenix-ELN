using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblExperimentTags
{
    public string GUID { get; set; } = null!;

    public string ExperimentID { get; set; } = null!;

    public string TagID { get; set; } = null!;

    public byte? SyncState { get; set; }

    public virtual tblExperiments Experiment { get; set; } = null!;

    public virtual tblTags Tag { get; set; } = null!;
}
