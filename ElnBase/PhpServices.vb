Imports System.Net
Imports System.Net.Http
Imports System.Runtime.InteropServices


''' <summary>
''' Provides infrastructure for interacting with publisher's server PHP scripts, e.g. 
''' for retrieving the most recent application version or to update installation statistics.
''' </summary>
'''
Public Class PhpServices

    ''' <summary>
    ''' Gets the most recent application version.
    ''' </summary>
    ''' <returns>Application version, e.g. 3.2.1.</returns>
    ''' 
    Public Shared Async Function GetLatestAppVersion() As Task(Of String)

        Dim res = Await GetPhpResult("https://chembytes.com/products/Phoenix_ELN/CurrentPhoenixVersion.php", Nothing)

        If res.StartsWith("Error:") Then
            Return ""
        Else
            Return res
        End If

    End Function


    ''' <summary>
    ''' Stores anonymous installation statistics to the publisher's database. This typically occurs on first 
    ''' installation and after each update installation.
    ''' </summary>
    ''' <param name="appVersion">The installed/upgraded application version</param>
    ''' <param name="expContext">The current local database context.</param>
    ''' 
    Public Shared Async Sub SendInstallInfo(appVersion As String, expContext As ElnDbContext, isServerEnabled As Boolean)

        Dim UTCOffset = TimeZoneInfo.Local.BaseUtcOffset.TotalHours.ToString()
        Dim nonDemoExpCount = Aggregate exp In expContext.tblExperiments Into Count(exp.UserID <> "demo")
        Dim dbGuid = expContext.tblDatabaseInfo.First.GUID
        Dim OSBits = If(Environment.Is64BitOperatingSystem, "64", "32")
        Dim OSVersion = RuntimeInformation.OSDescription.Replace("Microsoft Windows", "")

        Dim params As New Dictionary(Of String, String)
        With params
            .Add("dbGUID", dbGuid)
            .Add("appVersion", appVersion)
            .Add("timeZone", UTCOffset)
            .Add("OSVersion", OSVersion)
            .Add("OSBits", OSBits)
            .Add("expCount", nonDemoExpCount.ToString)
            .Add("serverEnabled", If(isServerEnabled, 1, 0))
        End With

        Await GetPhpResult("https://chembytes.com/products/Phoenix_ELN/PhoenixUse.php", params)   'upload info

    End Sub


    ''' <summary>
    ''' Gets the result string returned by the PHP script located at the specified URL path, using the 
    ''' specified parameters.
    ''' </summary>
    ''' 
    Friend Shared Async Function GetPhpResult(url As String, params As Dictionary(Of String, String)) As Task(Of String)

        Try

            Using webClient As New HttpClient

                Dim responseMsg As New HttpResponseMessage

                If Not IsNothing(params) Then
                    Dim encodedContent = New FormUrlEncodedContent(params)
                    responseMsg = Await webClient.PostAsync(url, encodedContent)
                Else
                    responseMsg = Await webClient.GetAsync(url)
                End If

                Return Await responseMsg.Content.ReadAsStringAsync

            End Using

        Catch ex As Exception

            Return "Error: Server Error."

        End Try

    End Function


End Class
