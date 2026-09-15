param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$alphaHeaders = @{ 'X-Development-User' = 'alpha-operator' }
$betaHeaders = @{ 'X-Development-User' = 'beta-operator' }
$body = @{ subject = 'Damaged monitor'; description = 'The monitor arrived broken.' } | ConvertTo-Json
$createdCase = Invoke-RestMethod "$BaseUrl/api/cases" -Method Post -Headers $alphaHeaders -ContentType 'application/json' -Body $body
$savedCase = Invoke-RestMethod "$BaseUrl/api/cases/$($createdCase.id)" -Headers $alphaHeaders
if ($savedCase.id -ne $createdCase.id) { throw 'Case read did not match creation.' }
try {
    Invoke-RestMethod "$BaseUrl/api/cases/$($createdCase.id)" -Headers $betaHeaders | Out-Null
    throw 'Cross-tenant access unexpectedly succeeded.'
} catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw }
}
$auditFacts = @()
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $auditFacts = @(Invoke-RestMethod "$BaseUrl/api/cases/$($createdCase.id)/audit" -Headers $alphaHeaders)
    if ($auditFacts.Count -gt 0) { break }
    Start-Sleep -Milliseconds 200
}
if ($auditFacts.Count -ne 1) { throw 'Expected exactly one case-created audit event.' }
[pscustomobject]@{
    CaseId = $savedCase.id
    Status = $savedCase.status
    CrossTenantStatus = 404
    AuditEvents = $auditFacts.Count
    ReadAfterRestart = "$BaseUrl/api/cases/$($savedCase.id)"
}
