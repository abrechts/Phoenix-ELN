using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblTags
{
    public string GUID { get; set; } = null!;

    public string DatabaseID { get; set; } = null!;

    public string TagName { get; set; } = null!;

    public byte? SyncState { get; set; }

    public virtual tblDatabaseInfo Database { get; set; } = null!;

    public virtual ICollection<tblExperimentTags> tblExperimentTags { get; set; } = new List<tblExperimentTags>();
}
