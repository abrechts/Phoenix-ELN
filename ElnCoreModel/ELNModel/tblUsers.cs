using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblUsers
{
    public string UserID { get; set; } = null!;

    public string DatabaseID { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? CompanyName { get; set; }

    public string? DepartmentName { get; set; }

    public string? City { get; set; }

    public string? PWHash { get; set; }

    public string? PWHint { get; set; }

    public byte IsSpellCheckEnabled { get; set; }

    public byte? IsCurrent { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblDatabaseInfo Database { get; set; } = null!;

    public virtual ICollection<tblExperiments> tblExperiments { get; set; } = new List<tblExperiments>();

    public virtual ICollection<tblProjects> tblProjects { get; set; } = new List<tblProjects>();
}
