Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Text.Json.Serialization
Imports System.Text.Json.Serialization.Metadata
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

Public Class ExperimentBase

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="dbContext"></param>
    ''' <param name="parentProject">The project entry to add the new experiment to.</param>
    ''' <param name="srcExperiment">The experiment entry to clone; can be nothing if creating an empty new experiment.</param>
    ''' <param name="cloneMethod">The CloneType specifying the cloning type. Ignored, if srcExperiment is nothing.</param>
    ''' <returns></returns>
    ''' 
    Public Shared Function AddExperiment(dbContext As ElnDataContext, parentProject As tblProjects, parentProjFolder As tblProjFolders,
       srcExperiment As tblExperiments, cloneMethod As CloneType) As tblExperiments

        If cloneMethod = CloneType.EmptyExperiment Then

            'Add an empty experiment

            Dim newExp As New tblExperiments

            With newExp
                .ExperimentID = GetNextExperimentID(parentProject.User)   'returns userID-00001 if no experiment present so far
                .User = parentProject.User
                .Project = parentProject
                .CreationDate = Now.ToString("yyyy-MM-dd HH:mm")
                .RefYieldFactor = 1
                .DisplayIndex = 0
                parentProject.tblExperiments.Add(newExp)
                parentProject.User.tblExperiments.Add(newExp)

            End With

            Return newExp

        Else

            'Clone the specified experiment

            Return CloneExperiment(dbContext, srcExperiment, parentProject, parentProjFolder, cloneMethod)

        End If

    End Function


    ''' <summary>
    ''' Creates a new experiment, or copies the specified experiment entry and adds it to the specified project with the highest 
    ''' available experiment ID.
    ''' </summary>
    ''' <param name="dbContext">The current database entity context.</param>
    ''' <param name="expEntry">The experiment entry to clone.</param>
    ''' <param name="dstProject">The destination project of the clone.</param>
    ''' <param name="dstProjFolder">The destination project subfolder of the clone.</param>
    ''' <param name="cloneMethod">The CloneType to apply.</param>
    ''' <param name="removeEmbedded">Removes all embedded items (images, files, etc.) from the clone. This 
    ''' parameter is ignored for all clone types except CloneType.FullExperiment.</param>
    ''' <returns>Cloned experiment entry, for reference only.</returns>
    ''' 
    Public Shared Function CloneExperiment(dbContext As ElnDataContext, expEntry As tblExperiments, dstProject As tblProjects,
      dstProjFolder As tblProjFolders, cloneMethod As CloneType, Optional removeEmbedded As Boolean = False) As tblExperiments

        Dim expCopy As tblExperiments

        'Clone or create new experiment entry
        '------------------------------------

        If cloneMethod = CloneType.EmptyExperiment Then
            expCopy = New tblExperiments
        Else
            expCopy = CType(dbContext.Entry(expEntry).CurrentValues.ToObject, tblExperiments)
        End If

        With expCopy
            .ExperimentID = GetNextExperimentID(dstProject.User)
            .Project = dstProject
            .ProjFolderID = dstProjFolder.GUID
            .User = dstProject.User
            .CreationDate = Now.ToString("yyyy-MM-dd HH:mm")
            .FinalizeDate = Nothing
            .UserTag = Nothing
            .WorkflowState = WorkflowStatus.InProgress
        End With

        dbContext.tblExperiments.Add(expCopy)


        'Clone the source experiment's tags. Restricted to clone types that carry over the actual protocol
        'content (FullExperiment, FromImport)
        '-------------------------------------------------------------------------------------------------------

        If cloneMethod = CloneType.FullExperiment OrElse cloneMethod = CloneType.FromImport Then

            For Each srcExpTag In expEntry.tblExperimentTags

                expCopy.tblExperimentTags.Add(New tblExperimentTags With {
                    .GUID = Guid.NewGuid.ToString("d"),
                    .Experiment = expCopy,
                    .Tag = srcExpTag.Tag
                })

            Next

        End If


        'Clone protocol items also (full experiment clone only)
        '-----------------------------------------------------

        If cloneMethod = CloneType.FullExperiment Then

            Dim navPropertyList = From nav In dbContext.Entry(New tblProtocolItems).Navigations Select nav.Metadata.PropertyInfo

            Dim protocolItems = From item In expEntry.tblProtocolItems Order By item.SequenceNr Ascending
            For Each protItem In protocolItems

                'clone protocol item
                '----------------------

                If protItem.tblEmbeddedFiles Is Nothing OrElse Not removeEmbedded Then

                    Dim protocolItemCopy = CType(dbContext.Entry(protItem).CurrentValues.ToObject, tblProtocolItems)
                    With protocolItemCopy
                        .GUID = Guid.NewGuid.ToString("d")
                        .Experiment = expCopy
                    End With

                    expCopy.tblProtocolItems.Add(protocolItemCopy)

                    For Each navProperty In navPropertyList ' protocolItemNavProperties

                        'clone protocol item content
                        '---------------------------

                        If navProperty.Name <> "Experiment" Then

                            Dim navTable = navProperty.GetValue(protItem)
                            If navTable IsNot Nothing Then

                                If navProperty.Name <> "tblProducts" Then

                                    If navTable.GetType().GetProperty("GUID") IsNot Nothing Then

                                        Dim navTblCopy = dbContext.Entry(navTable).CurrentValues.ToObject
                                        With navTblCopy
                                            .GUID = Guid.NewGuid.ToString("d")
                                            .ProtocolItem = protocolItemCopy
                                        End With
                                        navProperty.SetValue(protocolItemCopy, navTblCopy)

                                    End If

                                Else

                                    '- product placeholder 
                                    Dim prevProduct = protItem.tblProducts
                                    protocolItemCopy.TempInfo = "OrigProduct/" + prevProduct.ProductIndex.ToString + "/" +
                                       prevProduct.Yield.ToString("0.0") + "%"

                                    navProperty.SetValue(protocolItemCopy, Nothing)

                                End If

                            End If
                        End If
                    Next
                End If
            Next
        End If

        dbContext.SaveChanges() 'important for immediate UI display of contents

        Return expCopy

    End Function


    ''' <summary>
    ''' Deserializes the experiment from the specified Json export file and integrates it into the specified project.
    ''' </summary>
    ''' <returns>Nothing, if the process was cancelled or an error occurred.</returns>
    ''' 
    Public Shared Function ImportExperiment(dbContext As ElnDataContext, importPath As String, dstProject As tblProjects,
      dstProjFolder As tblProjFolders, currAppVersion As String) As tblExperiments

        Try
            Dim jsonStr = File.ReadAllText(importPath)
            Dim tagNames As List(Of String) = Nothing
            Dim importExp = ExperimentFromJsonString(jsonStr, currAppVersion, tagNames)
            If importExp IsNot Nothing Then

                'Resolve each imported tag name against the destination database's own tag set, creating it
                'if not already present there - tags are scoped per local database, so a tag exported from a
                'different database can't be reattached by GUID, only matched or recreated by name.
                Dim databaseID = dbContext.tblDatabaseInfo.First.GUID
                Dim existingTags = dbContext.tblTags.Where(Function(t) t.DatabaseID = databaseID).ToList()

                For Each tagName In tagNames

                    Dim destTag = existingTags.FirstOrDefault(Function(t) t.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                    If destTag Is Nothing Then
                        destTag = New tblTags With {
                            .GUID = Guid.NewGuid.ToString("d"),
                            .DatabaseID = databaseID,
                            .TagName = tagName
                        }
                        dbContext.tblTags.Add(destTag)
                        existingTags.Add(destTag)
                    End If

                    importExp.tblExperimentTags.Add(New tblExperimentTags With {
                        .GUID = Guid.NewGuid.ToString("d"),
                        .Experiment = importExp,
                        .Tag = destTag
                    })

                Next

                Dim newExp = CloneExperiment(dbContext, importExp, dstProject, dstProjFolder, CloneType.FullExperiment, removeEmbedded:=False)
                Return newExp
            Else
                Return Nothing
            End If

        Catch ex As Exception

            MsgBox(ex.Message, MsgBoxStyle.Information, "Import Error")
            Return Nothing

        End Try

    End Function


    ''' <summary>
    ''' Serializes the specified experiment entry to Json and writes its contents to the specified file path.
    ''' </summary>
    ''' <returns>False, if an error occurred, true otherwise.</returns>
    ''' <remarks>Import/Export only is guaranteed to work if the exporting app major/minor version is not  
    ''' higher then the current client version, since there may be database structure changes across versions.
    ''' </remarks>
    ''' 
    Public Shared Function ExportExperiment(expEntry As tblExperiments, exportFilePath As String, appVersion As String, dbContext As ElnDbContext) As Boolean

        Try

            Dim json = ExperimentToJsonString(expEntry, appVersion, dbContext)
            File.WriteAllText(exportFilePath, json)
            Return True

        Catch ex As Exception

            MsgBox(ex.Message, MsgBoxStyle.Information, "Export Error")
            Return False

        End Try

    End Function


    ''' <summary>
    ''' Unused (see remarks). Serializes specified experiment and child tables into Json string
    ''' </summary>
    ''' <param name="expEntry"></param>
    ''' <remarks>Import/Export only is guaranteed to work if the exporting app major/minor version is not  
    ''' higher then the current client version, since there may be database structure changes across versions.
    ''' </remarks>
    ''' 
    Public Shared Function ExperimentToJsonString(expEntry As tblExperiments, appVersion As String, dbContext As ElnDbContext) As String

        Dim jsonOptions As New JsonSerializerOptions
        jsonOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles
        jsonOptions.WriteIndented = True

        ' tblTags has a many-to-many relationship. TypeResolver prevents the indirect cyclic JSON reference tblExperimentTags -> 
        ' Tag -> Database -> tblTags (every tag in the DB) -> tblExperimentTags (every OTHER experiment using each tag) -> 
        Dim typeResolver As New DefaultJsonTypeInfoResolver
        typeResolver.Modifiers.Add(
            Sub(typeInfo As JsonTypeInfo)
                If typeInfo.Type Is GetType(tblExperiments) Then
                    Dim tagsProperty = typeInfo.Properties.FirstOrDefault(Function(p) p.Name = "tblExperimentTags")
                    If tagsProperty IsNot Nothing Then
                        typeInfo.Properties.Remove(tagsProperty)
                    End If
                End If
            End Sub)
        jsonOptions.TypeInfoResolver = typeResolver

        ' Collect tag names before detaching the graph below.These are resolved or recreated by name in
        ' the destination database on import instead (see ExperimentFromJsonString).
        Dim tagNames = expEntry.tblExperimentTags.Select(Function(et) et.Tag.TagName).ToList()

        ' clone current expEntry for subsequent changes
        Dim expCopy = CType(dbContext.Entry(expEntry).CurrentValues.ToObject, tblExperiments)

        ' break upstream references of experiment to prevent serializing complete database
        With expCopy
            .UserID = Nothing
            .ProjectID = Nothing
            .ProjFolderID = Nothing
        End With

        Dim jsonStr = JsonSerializer.Serialize(expCopy, jsonOptions)

        'append app version and tag name properties (tag names needed to reattach tags on import)
        Dim jsonObject As JsonObject = JsonSerializer.Deserialize(Of JsonObject)(jsonStr)
        jsonObject.Add("AppVersion", appVersion)
        jsonObject.Add("TagNames", JsonSerializer.SerializeToNode(tagNames))
        jsonStr = JsonSerializer.Serialize(jsonObject)

        Return jsonStr

    End Function


    ''' <summary>
    ''' Gets an experiment entry from a json string; returns nothing in case of an error.
    ''' </summary>
    ''' <param name="tagNames">Receives the source experiment's tag names (empty for an export from a
    ''' version predating tags, or if it had none). See ExperimentToJsonString for why tags travel as
    ''' plain names rather than serialized entities.</param>
    '''
    Public Shared Function ExperimentFromJsonString(jsonString As String, currAppVersion As String, ByRef tagNames As List(Of String)) As tblExperiments

        tagNames = New List(Of String)

        Try

            Dim jsonObj = JsonSerializer.Deserialize(Of JsonObject)(jsonString)
            Dim exportAppVersion As String = ""
            jsonObj.TryGetPropertyValue("AppVersion", exportAppVersion)
            jsonObj.Remove("AppVersion")

            Dim tagNamesNode As JsonNode = Nothing
            If jsonObj.TryGetPropertyValue("TagNames", tagNamesNode) AndAlso tagNamesNode IsNot Nothing Then
                tagNames = JsonSerializer.Deserialize(Of List(Of String))(tagNamesNode.ToJsonString())
            End If
            jsonObj.Remove("TagNames")

            Dim currVersion = New Version(currAppVersion)
            Dim exportVersion = New Version(exportAppVersion)

            'only consider major and minor versions (assumes that db schema changes occur at least in minor version changes)
            Dim currVal = 100 * currVersion.Major + currVersion.Minor
            Dim exportVal = 100 * exportVersion.Major + exportVersion.Minor
            If exportVal > currVal Then
                MsgBox("Sorry, can't import experiments created by a " + vbCrLf +
                       "higher application version. Please update to " + vbCrLf +
                       "the most recent ELN version, then try again.", MsgBoxStyle.Information, "Import Conflict")
                Return Nothing
            End If

            Return JsonSerializer.Deserialize(Of tblExperiments)(jsonObj)

        Catch ex As Exception
            Return Nothing
        End Try

    End Function


    ''' <summary>
    ''' Gets the most recent experiment, or returns nothing if no experiment present so far.
    ''' </summary>
    ''' 
    Public Shared Function GetLatestExperiment(currUser As tblUsers) As tblExperiments

        Return (From exp In currUser.tblExperiments Order By exp.ExperimentID Descending).FirstOrDefault

    End Function


    ''' <summary>
    ''' Gets the next available incremental experimentID for the current user. 
    ''' </summary>
    ''' <returns></returns>
    ''' 
    Public Shared Function GetNextExperimentID(userEntry As tblUsers) As String

        Dim maxExperiment = GetLatestExperiment(userEntry)
        If maxExperiment IsNot Nothing Then
            Dim maxVal = CInt(maxExperiment.ExperimentID.Substring(maxExperiment.ExperimentID.Length - 4, 4))
            Return userEntry.UserID + "-" + Format((maxVal + 1), "00000")
        Else
            'no experiment present so far
            Return userEntry.UserID + "-00001"
        End If

    End Function

End Class
