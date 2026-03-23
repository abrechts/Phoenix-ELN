Imports System.IO
Imports System.Text.Json
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class ReactionQuery

    Public Class RSSQueryParameters

        Public Property ReactionSketchXml As String = ""
        Public Property UserID As String = ""
        Public Property ProjectName As String = ""
        Public Property ProjectGroupName As String = ""
        Public Property IsYieldSmallerOrEqual As Boolean = False
        Public Property YieldValue As Double? = Nothing
        Public Property ScaleValue As Double? = Nothing
        Public Property IsScaleSmallerOrEqual As Boolean = False
        '  Public Property IsServerQuery As Boolean = False

    End Class


    Public Property SearchContext As ElnDbContext   'assigned rdoLocal and rdoServer setting

    Public Property IsServerQuery As Boolean = False

    Public Shared Property LastFilterValidationError As Boolean = False



    Public Sub New(searchContext As ElnDbContext, isServerQuery As Boolean)

        Me.SearchContext = searchContext
        Me.IsServerQuery = isServerQuery

    End Sub


    ''' <summary>
    ''' Filters the provided RSS hits based on the provided RSS queryParams and returns the resulting experiment entries.
    ''' </summary>
    ''' 
    Public Function FilterRssHits(rssHits As IEnumerable(Of tblExperiments), queryParams As RSSQueryParameters) As IEnumerable(Of tblExperiments)

        ' quit if no hits to filter
        If rssHits Is Nothing Then
            Return Enumerable.Empty(Of tblExperiments)()
        End If

        With queryParams

            ' -- userID 
            If Not String.IsNullOrEmpty(.UserID) Then
                rssHits = rssHits.Where(Function(x) x.UserID = .UserID)
            End If

            ' -- project name
            If Not String.IsNullOrEmpty(.ProjectName) Then
                rssHits = rssHits.Where(Function(x) x.Project.Title.ToLower = .ProjectName.ToLower)
            End If

            ' -- project group name
            If Not String.IsNullOrEmpty(.ProjectGroupName) Then
                rssHits = rssHits.Where(Function(x) x.ProjFolder.FolderName.ToLower = .ProjectGroupName.ToLower)
            End If

            ' -- yield range
            If .YieldValue IsNot Nothing Then
                If .IsYieldSmallerOrEqual Then
                    rssHits = rssHits.Where(Function(x) x.Yield Is Nothing OrElse x.Yield <= .YieldValue)   'also includes no yield experiments
                Else
                    rssHits = rssHits.Where(Function(x) x.Yield IsNot Nothing AndAlso x.Yield >= .YieldValue)
                End If
            End If

            ' -- scale range
            If .ScaleValue IsNot Nothing Then
                If .IsScaleSmallerOrEqual = True Then
                    rssHits = rssHits.Where(Function(x) x.RefReactantGrams <= .ScaleValue)
                Else
                    rssHits = rssHits.Where(Function(x) x.RefReactantGrams >= .ScaleValue)
                End If
            End If

        End With

        Return rssHits

    End Function


    ''' <summary>
    ''' Saves the provided RSS query parameters to a specified file path in JSON format.  
    ''' </summary>
    ''' 
    Public Shared Sub Save(storedQuery As RSSQueryParameters, filePath As String)

        Dim json = JsonSerializer.Serialize(storedQuery, DefaultOptions)
        File.WriteAllText(filePath, json)

    End Sub


    ''' <summary>
    ''' Loads RSS query parameters from a specified file path in JSON format and returns them as an instance of RSSQueryDefinitions. 
    ''' If the file does not exist, the function returns Nothing.
    ''' </summary>
    '''
    Public Shared Function Load(searchContext As ElnDbContext, filePath As String) As RSSQueryParameters

        If Not File.Exists(filePath) Then Return Nothing

        Dim json = File.ReadAllText(filePath)
        Dim res = JsonSerializer.Deserialize(Of RSSQueryParameters)(json, DefaultOptions)

        Return ValidateLocationFilters(searchContext, res)

    End Function


    ''' <summary>
    ''' Defines default JSON serialization options.
    ''' </summary>
    ''' 
    Private Shared ReadOnly DefaultOptions As New JsonSerializerOptions With {
           .WriteIndented = True,
           .PropertyNameCaseInsensitive = True
        }


    ''' <summary>
    ''' A stored query may contain userID, projectName and projectGroupName values that are not found in the current search context. 
    ''' This function determines if userID or projectName as specified in origParams exist, and if a specified project contains 
    ''' the specified projectGroupName. Invalid items are set to empty string in the returned RSSQueryParameters object and the 
    ''' global LastFilterError is set accordingly.
    ''' </summary>
    ''' 
    ''' 
    Private Shared Function ValidateLocationFilters(searchContext As ElnDbContext, origParams As RSSQueryParameters) As RSSQueryParameters

        Dim projectEntry As tblProjects = Nothing
        LastFilterValidationError = False

        If origParams.UserID <> String.Empty Then

            Dim userEntry = searchContext.tblUsers.Where(Function(u) u.UserID = origParams.UserID).FirstOrDefault
            If userEntry Is Nothing Then
                origParams.UserID = String.Empty
                LastFilterValidationError = True
            End If

        End If

        If origParams.ProjectName <> String.Empty Then

            projectEntry = searchContext.tblProjects.Where(Function(p) p.Title.ToLower = origParams.ProjectName.ToLower).FirstOrDefault
            If projectEntry Is Nothing Then
                origParams.ProjectName = String.Empty
                origParams.ProjectGroupName = String.Empty 'since group depends on parent project
                LastFilterValidationError = True
            End If

        End If

        If projectEntry IsNot Nothing AndAlso origParams.ProjectGroupName <> String.Empty Then  'group depends on parent project

            Dim groupEntry = projectEntry.tblProjFolders.Where(Function(p) p.FolderName.ToLower = origParams.ProjectGroupName.ToLower).FirstOrDefault
            If groupEntry Is Nothing Then
                origParams.ProjectGroupName = String.Empty
                LastFilterValidationError = True
            End If

        End If

        Return origParams

    End Function



End Class
