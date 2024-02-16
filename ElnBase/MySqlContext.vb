
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore
Imports MySqlConnector

Public Class MySqlContext

    ''' <summary>
    ''' Create a new MySql server data context.
    ''' </summary>
    ''' 
    Public Sub New(serverName As String, userID As String, password As String, dbName As String, Optional port As Integer = 3306)

        Dim serverConnStr As String

        Dim connBuilder As New MySqlConnectionStringBuilder
        With connBuilder
            .Server = serverName
            .Port = port
            .Database = dbName
            .UserID = userID
            .Password = password
            .ConnectionIdleTimeout = 5
            .ConnectionTimeout = 5
            serverConnStr = .ToString
        End With

        Dim optionsBuilder As New DbContextOptionsBuilder(Of ElnDataContext)
        Dim opts = optionsBuilder.UseMySql(serverConnStr, ServerVersion.AutoDetect(serverConnStr))

        'Important! Lazy loading required to obtain populated hierarchical navigation model data.
        'Install Microsoft.EntityFrameworkCore.Proxies NuGet package to use the option below:
        opts.UseLazyLoadingProxies

        ElnServerContext = New ElnDbContext(opts.Options)

    End Sub


    Public Sub New(serverConnStr As String)

        Dim optionsBuilder As New DbContextOptionsBuilder(Of ElnDataContext)
        Dim opts = optionsBuilder.UseMySql(serverConnStr, ServerVersion.AutoDetect(serverConnStr))

        ' Important! Lazy loading required to obtain populated hierarchical navigation model data.
        '   Install Microsoft.EntityFrameworkCore.Proxies NuGet package to use the option below
        opts.UseLazyLoadingProxies

        ElnServerContext = New ElnDbContext(opts.Options)

    End Sub


    ''' <summary>
    ''' Sets or gets the ElnDataContext of the entity model.
    ''' </summary>
    ''' 
    Public Property ElnServerContext As ElnDbContext


End Class
