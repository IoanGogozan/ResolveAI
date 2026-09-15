param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$headers = @{ 'X-Development-User' = 'alpha-operator' }
$request = @{
    subject = 'Damaged monitor'
    description = 'The monitor arrived broken.'
    references = @{ customerId = 'customer-monitor'; orderId = 'order-monitor'; paymentId = 'payment-monitor' }
}
$case = Invoke-RestMethod "$BaseUrl/api/cases" -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json)
$facts = Invoke-RestMethod "$BaseUrl/api/cases/$($case.id)/business-facts" -Headers $headers
if ($facts.customer.id -ne 'customer-monitor' -or $facts.order.id -ne 'order-monitor' -or $facts.payment.id -ne 'payment-monitor') {
    throw 'Business facts do not match the case references.'
}
$request.references.paymentId = 'payment-other'
try {
    Invoke-RestMethod "$BaseUrl/api/cases" -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json) | Out-Null
    throw 'Mismatched payment unexpectedly accepted.'
} catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 422) { throw }
}
[pscustomobject]@{
    CaseId = $case.id
    Customer = $facts.customer.displayName
    Order = $facts.order.id
    CapturedAmount = $facts.payment.capturedAmount
    Currency = $facts.payment.currency
    MismatchedPaymentStatus = 422
}
