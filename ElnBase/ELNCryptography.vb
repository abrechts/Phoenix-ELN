Imports System.Security.Cryptography
Imports System.Text


Public Class ELNCryptography

    ''' <summary>
    ''' Gets the SHA-256 hash of the specified string as lower-case hexadecimal string.
    ''' </summary>
    ''' <remarks>
    ''' Dynamically adapts to local FIPS compliance setting.
    ''' </remarks>
    '''
    Public Shared Function GetSHA256Hash(ByVal contentStr As String) As String

        Dim encoder = New UTF8Encoding
        Dim contentBytes = encoder.GetBytes(contentStr)

        Return GetSHA256Hash(contentBytes)

    End Function


    ''' <summary>
    ''' Gets the SHA-256 hash of the specified byte array as lower-case hexadecimal string.
    ''' </summary>
    ''' <remarks>
    ''' Dynamically adapts to local FIPS compliance setting.
    ''' </remarks>
    '''
    Public Shared Function GetSHA256Hash(ByVal contentBytes As Byte()) As String

        Using SHA256Hasher = SHA256.Create  'this is FIPS compliant

            Dim hashedBytes As Byte() = SHA256Hasher.ComputeHash(contentBytes)
            SHA256Hasher.Clear()
            Return ByteArrToHexString(hashedBytes)

        End Using

    End Function


    ''' <summary>
    ''' Converts the specified byte array into a lower-case hexadecimal string
    ''' </summary>
    ''' 
    Public Shared Function ByteArrToHexString(byteArr As Byte()) As String

        Dim strBuilder As New StringBuilder()

        For Each b In byteArr
            strBuilder.AppendFormat("{0:x2}", b)
        Next

        Return strBuilder.ToString()

    End Function

End Class
