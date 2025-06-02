Imports ElnBase
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

Public Class ProjectFolders

    ''' <summary>
    ''' The default title assigned to the first folder of a project.
    ''' </summary>
    ''' 
    Public Shared Property DefaultFolderTitle As String = "All experiments"


    ''' <summary>
    ''' Performs one-time initializing project folder integration after db upgrade
    ''' </summary>
    ''' 
    Public Shared Sub Initialize(localContext As ElnDbContext)

        For Each proj In localContext.tblProjects
            If proj.tblProjFolders.Count = 0 Then

                Dim folderEntry = Add(proj, DefaultFolderTitle, localContext)

                For Each exp In proj.tblExperiments
                    If exp.ProjFolderID Is Nothing Then
                        exp.ProjFolderID = folderEntry.GUID
                    End If
                Next

            End If
        Next

        localContext.SaveChanges()

    End Sub


    ''' <summary>
    ''' Initializes restored DB originally containing pre-projFolder data, but already upgraded db schema
    ''' with still missing exp -> projFolder references.
    ''' </summary>
    '''
    Public Shared Sub SetMissingProjFolderRefs(localContext As ElnDataContext)

        'determine if missing refs are present
        Dim res = From exp In localContext.tblExperiments Where exp.ProjFolderID Is Nothing
        If Not res.Any Then
            Exit Sub
        End If

        For Each exp In localContext.tblExperiments
            If exp.ProjFolder Is Nothing Then
                exp.ProjFolderID = exp.Project.tblProjFolders.First.GUID
            End If
        Next

    End Sub


    ''' <summary>
    ''' Adds an empty subfolder to the specified project.
    ''' </summary>
    ''' 
    Public Shared Function Add(dstProject As tblProjects, folderTitle As String, localContext As ElnDbContext)

        Dim projFolderEntry As New tblProjFolders
        With projFolderEntry

            .GUID = Guid.NewGuid.ToString("d")
            .ProjectID = dstProject.GUID
            .FolderName = folderTitle
            .SequenceNr = dstProject.tblProjFolders.Count
            .IsNodeExpanded = 1

            dstProject.tblProjFolders.Add(projFolderEntry)

            Return projFolderEntry

        End With

    End Function

End Class
