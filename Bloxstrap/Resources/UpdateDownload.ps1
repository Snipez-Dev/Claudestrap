param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination,
    [Parameter(Mandatory=$true)][string]$JobName,
    [Parameter(Mandatory=$true)][string]$Digest,
    [Parameter(Mandatory=$true)][long]$ExpectedSize,
    [int]$ParentPid = 0,
    [long]$ParentStartedTicks = 0,
    [int]$NoProgressSeconds = 120
)
$ErrorActionPreference = 'Stop'
$job = $null
function Report([string]$State, [long]$Bytes, [long]$Total, [string]$Message = '') {
    @{ State=$State; Bytes=$Bytes; Total=$Total; Message=$Message } | ConvertTo-Json -Compress | Write-Output
}
try {
    Import-Module BitsTransfer -ErrorAction Stop
    $description = "Claudestrap SHA256:$Digest size:$ExpectedSize"
    $job = Get-BitsTransfer -Name $JobName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($job) {
        $localFiles = @($job.FileList)
        if ($job.Description -ne $description -or $localFiles.Count -ne 1 -or
            [IO.Path]::GetFullPath($localFiles[0].LocalName) -ne [IO.Path]::GetFullPath($Destination) -or
            $localFiles[0].RemoteName -ne $Source) {
            throw 'An existing BITS job has different metadata; it has not been changed.'
        }
    } else {
        $job = Start-BitsTransfer -Source $Source -Destination $Destination -DisplayName $JobName `
            -Description $description -TransferType Download -Priority Foreground -Asynchronous `
            -RetryInterval 60 -RetryTimeout 86400 -CustomHeaders 'User-Agent: Claudestrap-Updater' -ErrorAction Stop
    }
    Report 'Attached' 0 $ExpectedSize
    $lastBytes = -1L
    $lastProgress = [DateTime]::UtcNow
    while ($true) {
        if ($ParentPid -gt 0) {
            try {
                $parent = Get-Process -Id $ParentPid -ErrorAction Stop
                if ($parent.StartTime.ToUniversalTime().Ticks -ne $ParentStartedTicks) { exit 5 }
            } catch { exit 5 }
        }
        $job = Get-BitsTransfer -JobId $job.JobId -ErrorAction Stop
        $bytes = [long]$job.BytesTransferred
        if ($bytes -ne $lastBytes) { $lastBytes=$bytes; $lastProgress=[DateTime]::UtcNow }
        Report ([string]$job.JobState) $bytes $ExpectedSize
        switch ([string]$job.JobState) {
            'Transferred' {
                Complete-BitsTransfer -BitsJob $job -ErrorAction Stop
                Report 'Completed' $bytes $ExpectedSize
                exit 0
            }
            'Suspended' { Resume-BitsTransfer -BitsJob $job -Asynchronous -ErrorAction Stop | Out-Null }
            'Error' {
                $message = [string]$job.ErrorDescription
                Remove-BitsTransfer -BitsJob $job -Confirm:$false -ErrorAction Stop
                Report 'Unavailable' $bytes $ExpectedSize $message
                exit 2
            }
            'Cancelled' { throw 'The update download was cancelled.' }
        }
        if (([DateTime]::UtcNow - $lastProgress).TotalSeconds -ge $NoProgressSeconds) {
            Report 'Pending' $bytes $ExpectedSize 'BITS download retained. Open the updater again to reconnect to it.'
            exit 4
        }
        Start-Sleep -Milliseconds 500
    }
} catch {
    if ($null -eq $job) { Report 'Unavailable' 0 $ExpectedSize $_.Exception.Message; exit 2 }
    Report 'Error' 0 $ExpectedSize $_.Exception.Message
    exit 3
}
