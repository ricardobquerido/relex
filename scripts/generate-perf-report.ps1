param(
    [string]$BaseUrl = "http://localhost:5080",
    [ValidateSet("small","medium","large","xlarge")]
    [string]$IngestionPreset = "medium",
    [int]$IngestionParts = 4,
    [int]$WarmupRequests = 5,
    [int]$Samples = 20,
    [int]$BulkCount = 10000,
    [string]$OutputPath = "perf-report.md",
    [switch]$SkipIngestion,
    [switch]$SkipApi,
    [switch]$SkipBulk
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

try {
    Add-Type -AssemblyName System.Net.Http
} catch {
    # ignore; 
}

function Get-LookupCode {
    param(
        [Parameter(Mandatory = $true)]$Item,
        [Parameter(Mandatory = $true)][string]$EndpointName
    )

    if ($null -eq $Item) { return $null }

    try {
        $codeProp = $Item.PSObject.Properties["code"]
        if ($null -ne $codeProp) { return [string]$codeProp.Value }
    } catch { }

    try {
        $codeProp2 = $Item.PSObject.Properties["Code"]
        if ($null -ne $codeProp2) { return [string]$codeProp2.Value }
    } catch { }

    if ($Item -is [System.Collections.IDictionary]) {
        if ($Item.Contains("code")) { return [string]$Item["code"] }
        if ($Item.Contains("Code")) { return [string]$Item["Code"] }
    }

    throw "Unexpected payload shape from $EndpointName. Expected items with a 'code' property. Actual keys/properties: $($Item.PSObject.Properties.Name -join ', ')"
}

function To-ItemArray {
    param(
        [Parameter(Mandatory = $true)]$Value
    )

    if ($null -eq $Value) { return @() }

    if ($Value -is [System.Array]) { return $Value }

    return @($Value)
}

function Get-Median([double[]]$values) {
    if (-not $values -or $values.Count -eq 0) { return $null }
    $sorted = $values | Sort-Object
    $mid = [int]($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 0) {
        return ($sorted[$mid - 1] + $sorted[$mid]) / 2
    }
    return $sorted[$mid]
}

function Get-P95([double[]]$values) {
    if (-not $values -or $values.Count -eq 0) { return $null }
    $sorted = $values | Sort-Object
    $idx = [int][Math]::Ceiling(0.95 * $sorted.Count) - 1
    if ($idx -lt 0) { $idx = 0 }
    if ($idx -ge $sorted.Count) { $idx = $sorted.Count - 1 }
    return $sorted[$idx]
}

function Measure-HttpMs([scriptblock]$action) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $action | Out-Null
    $sw.Stop()
    return $sw.Elapsed.TotalMilliseconds
}

function Require-Ok([string]$url) {
    try {
        Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10 | Out-Null
    } catch {
        throw "Failed to reach '$url'. Ensure the API is running and reachable. Inner: $($_.Exception.Message)"
    }
}

function Format-DateOnly([datetime]$dt) {
    return $dt.ToString("yyyy-MM-dd")
}

function New-HttpClient {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(60)
    return $client
}

function Invoke-Http {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$JsonBody
    )

    $client = New-HttpClient
    try {
        $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Url)
        $req.Headers.Accept.ParseAdd("application/json")

        if ($null -ne $JsonBody) {
            $req.Content = [System.Net.Http.StringContent]::new($JsonBody, [System.Text.Encoding]::UTF8, "application/json")
        }

        $resp = $client.SendAsync($req).GetAwaiter().GetResult()
        $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ReasonPhrase = $resp.ReasonPhrase
            Headers = $resp.Headers
            Body = $body
            IsSuccessStatusCode = $resp.IsSuccessStatusCode
        }
    }
    finally {
        $client.Dispose()
    }
}

function Invoke-HttpJson {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$JsonBody
    )

    $resp = Invoke-Http -Method $Method -Url $Url -JsonBody $JsonBody

    if ($resp.StatusCode -in 301,302,303,307,308) {
        $location = $resp.Headers.Location
        $locStr = if ($location) { $location.ToString() } else { "(missing Location header)" }
        throw "Request to '$Url' was redirected ($($resp.StatusCode)) to '$locStr'. Your API likely enforces HTTPS (UseHttpsRedirection). Re-run with -BaseUrl set to the HTTPS address (e.g. https://localhost:5081)."
    }

    if (-not $resp.IsSuccessStatusCode) {
        throw "HTTP $($resp.StatusCode) $($resp.ReasonPhrase). Body: $($resp.Body)"
    }

    if ([string]::IsNullOrWhiteSpace($resp.Body)) {
        return $null
    }

    try {
        return ($resp.Body | ConvertFrom-Json)
    } catch {
        throw "Expected JSON but could not parse response. Body: $($resp.Body)"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("## Performance Observations (Local Benchmark)")
$reportLines.Add("")
$reportLines.Add("_Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")_")
$reportLines.Add("")
$reportLines.Add("Environment:")
$reportLines.Add("- OS: $([System.Environment]::OSVersion.VersionString)")
$reportLines.Add("- CPU: $((Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name))")
$reportLines.Add("- RAM (GB): $([Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2))")
$reportLines.Add("")

$ingestionSeconds = $null
$ingestionRate = $null

if (-not $SkipIngestion) {
    $loadSh = Join-Path $repoRoot "ingestion\load.sh"
    if (-not (Test-Path $loadSh)) {
        $reportLines.Add("Ingestion: skipped (missing ingestion/load.sh)")
        $reportLines.Add("")
    } else {
        $bash = Get-Command bash -ErrorAction SilentlyContinue
        if (-not $bash) {
            $reportLines.Add("Ingestion: skipped (bash not found). Run via Git Bash/WSL or set -SkipIngestion.")
            $reportLines.Add("")
        } else {
            try {
                & bash --version 2>$null | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "bash exited with code $LASTEXITCODE" }
            } catch {
                $reportLines.Add("Ingestion: skipped (bash is present but not runnable). Install Git Bash/WSL properly or use -SkipIngestion.")
                $reportLines.Add("")
                $bash = $null
            }

            if (-not $bash) {
                # already reported
            } else {
            $reportLines.Add("Running ingestion: $IngestionPreset ($IngestionParts parts)")
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            & bash $loadSh $IngestionPreset $IngestionParts 2>$null
            $sw.Stop()
            $ingestionSeconds = $sw.Elapsed.TotalSeconds

            $rows = switch ($IngestionPreset) {
                "small" { 100000 }
                "medium" { 1000000 }
                "large" { 10000000 }
                "xlarge" { 100000000 }
                default { $null }
            }

            if ($rows -ne $null -and $ingestionSeconds -gt 0) {
                $ingestionRate = [Math]::Round($rows / $ingestionSeconds, 0)
            }

            $reportLines.Add("Ingestion completed in ~$([Math]::Round($ingestionSeconds, 2))s")
            if ($ingestionRate) {
                $reportLines.Add("Approx throughput: ~$ingestionRate rows/s")
            }
            $reportLines.Add("")
            }
        }
    }
}

$healthUrl = "$BaseUrl/health"

$createMs = $null
$healthMs = $null
$locationsSamples = @()
$productsSamples = @()
$statsSamples = @()
$getSamples = @()
$getNoDateSamples = @()
$listSamples = @()
$updateMs = $null
$deleteMs = $null
$bulkMs = $null
$createdOrder = $null
$createdOrderForDelete = $null

if (-not $SkipApi) {
    $healthMs = Measure-HttpMs { Require-Ok $healthUrl }

    $locationsUrl = "$BaseUrl/locations"
    $productsUrl = "$BaseUrl/products"
    $statsUrl = "$BaseUrl/orders/stats"

    $locations = To-ItemArray (Invoke-RestMethod -Uri $locationsUrl -Method Get)
    if ((@($locations)).Count -lt 1) {
        throw "No locations found at '$locationsUrl'. Your DB is reachable, but lookup seed data is missing. Run the ingestion pipeline (or at least load lookups/locations.csv and lookups/products.csv) and restart the API so the cache warms up."
    }

    $products = To-ItemArray (Invoke-RestMethod -Uri $productsUrl -Method Get)
    if ((@($products)).Count -lt 1) {
        throw "No products found at '$productsUrl'. Your DB is reachable, but lookup seed data is missing. Run the ingestion pipeline (or at least load lookups/locations.csv and lookups/products.csv) and restart the API so the cache warms up."
    }

    $firstLocation = ($locations | Select-Object -First 1)
    $firstProduct = ($products | Select-Object -First 1)
    $locationCode = Get-LookupCode -Item $firstLocation -EndpointName $locationsUrl
    $productCode = Get-LookupCode -Item $firstProduct -EndpointName $productsUrl
    if ([string]::IsNullOrWhiteSpace($locationCode) -or [string]::IsNullOrWhiteSpace($productCode)) {
        throw "Invalid lookup payload from '$locationsUrl'/'$productsUrl'. Expected items with a 'code' property."
    }

    $tomorrow = (Get-Date).ToUniversalTime().Date.AddDays(1)
    $tomorrowStr = Format-DateOnly $tomorrow

    $createPayload = @{
        locationCode = $locationCode
        productCode  = $productCode
        orderDate    = $tomorrowStr
        quantity     = 100
        submittedBy  = "PerfReport"
    }

    $createUrl = "$BaseUrl/orders"
    $createSw = [System.Diagnostics.Stopwatch]::StartNew()
    $createResp = Invoke-Http -Method "POST" -Url $createUrl -JsonBody ($createPayload | ConvertTo-Json)
    $createSw.Stop()
    $createMs = $createSw.Elapsed.TotalMilliseconds

    if ($createResp.StatusCode -in 301,302,303,307,308) {
        $location = $createResp.Headers.Location
        $locStr = if ($location) { $location.ToString() } else { "(missing Location header)" }
        throw "Request to '$createUrl' was redirected ($($createResp.StatusCode)) to '$locStr'. Your API likely enforces HTTPS. Re-run with -BaseUrl set to the HTTPS address (e.g. https://localhost:5081)."
    }
    if (-not $createResp.IsSuccessStatusCode) {
        throw "CreateOrder failed. HTTP $($createResp.StatusCode) $($createResp.ReasonPhrase). Body: $($createResp.Body)"
    }

    if (-not [string]::IsNullOrWhiteSpace($createResp.Body)) {
        $createdOrder = ($createResp.Body | ConvertFrom-Json)
    } else {
        $loc = $createResp.Headers.Location
        if ($null -ne $loc) {
            $m = [regex]::Match($loc.ToString(), "([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")
            if ($m.Success) {
                $createdOrder = [pscustomobject]@{ id = $m.Groups[1].Value; orderDate = $createPayload.orderDate }
            }
        }

        if (-not $createdOrder) {
            $locStr2 = if ($loc) { $loc.ToString() } else { "<missing>" }
            throw "CreateOrder returned an empty body and no parsable id from Location header. HTTP $($createResp.StatusCode) $($createResp.ReasonPhrase). Location: $locStr2"
        }
    }

    if (-not $createdOrder -or -not $createdOrder.id) {
        $actualType = if ($createdOrder) { $createdOrder.GetType().FullName } else { "<null>" }
        throw "CreateOrder did not return an object with an 'id'. Parsed type: $actualType. If you're using HTTP, try HTTPS (UseHttpsRedirection), or check your API response for errors."
    }

    $orderId = $createdOrder.id
    $orderDate = $createdOrder.orderDate
    if (-not $orderDate) { $orderDate = $createPayload.orderDate }

    $getUrl = "$BaseUrl/orders/${orderId}?orderDate=$orderDate"
    $getNoDateUrl = "$BaseUrl/orders/$orderId"
    $listUrl = "$BaseUrl/orders?page=1&pageSize=20"

    $statsRangeStart = Format-DateOnly ((Get-Date).ToUniversalTime().Date.AddDays(-30))
    $statsRangeEnd = Format-DateOnly ((Get-Date).ToUniversalTime().Date.AddDays(30))
    $statsUrlWithRange = "${statsUrl}?startDate=$statsRangeStart&endDate=$statsRangeEnd"

    for ($i = 0; $i -lt $WarmupRequests; $i++) {
        Invoke-RestMethod -Uri $locationsUrl -Method Get | Out-Null
        Invoke-RestMethod -Uri $productsUrl -Method Get | Out-Null
        Invoke-RestMethod -Uri $statsUrlWithRange -Method Get | Out-Null
        Invoke-RestMethod -Uri $getUrl -Method Get | Out-Null
        Invoke-RestMethod -Uri $getNoDateUrl -Method Get | Out-Null
        Invoke-RestMethod -Uri $listUrl -Method Get | Out-Null
    }

    for ($i = 0; $i -lt $Samples; $i++) {
        $locationsSamples += Measure-HttpMs { Invoke-RestMethod -Uri $locationsUrl -Method Get }
        $productsSamples += Measure-HttpMs { Invoke-RestMethod -Uri $productsUrl -Method Get }
        $statsSamples += Measure-HttpMs { Invoke-RestMethod -Uri $statsUrlWithRange -Method Get }
        $getSamples += Measure-HttpMs { Invoke-RestMethod -Uri $getUrl -Method Get }
        $getNoDateSamples += Measure-HttpMs { Invoke-RestMethod -Uri $getNoDateUrl -Method Get }
        $listSamples += Measure-HttpMs { Invoke-RestMethod -Uri $listUrl -Method Get }
    }

    # Update endpoint (PUT /orders/{id})
    # Keep status pending by default so delete benchmark can still succeed (update is quantity-only).
    $updateUrl = "$BaseUrl/orders/$orderId"
    $updatePayload = @{
        quantity = 101
        orderDate = $orderDate
    }
    $updateMs = Measure-HttpMs {
        Invoke-RestMethod -Uri $updateUrl -Method Put -ContentType "application/json" -Body ($updatePayload | ConvertTo-Json) | Out-Null
    }

    # Delete endpoint (DELETE /orders/{id})
    # Create a separate order to delete so the update benchmark cannot influence the delete rule set.
    $createPayloadForDelete = @{
        locationCode = $locationCode
        productCode  = $productCode
        orderDate    = $tomorrowStr
        quantity     = 1
        submittedBy  = "PerfReport"
    }
    $createResp2 = Invoke-Http -Method "POST" -Url $createUrl -JsonBody ($createPayloadForDelete | ConvertTo-Json)
    if ($createResp2.StatusCode -in 301,302,303,307,308) {
        $location = $createResp2.Headers.Location
        $locStr = if ($location) { $location.ToString() } else { "(missing Location header)" }
        throw "Request to '$createUrl' was redirected ($($createResp2.StatusCode)) to '$locStr'. Your API likely enforces HTTPS. Re-run with -BaseUrl set to the HTTPS address (e.g. https://localhost:5081)."
    }
    if (-not $createResp2.IsSuccessStatusCode) {
        throw "CreateOrder (for delete) failed. HTTP $($createResp2.StatusCode) $($createResp2.ReasonPhrase). Body: $($createResp2.Body)"
    }

    if (-not [string]::IsNullOrWhiteSpace($createResp2.Body)) {
        $createdOrderForDelete = ($createResp2.Body | ConvertFrom-Json)
    } else {
        $loc2 = $createResp2.Headers.Location
        if ($null -ne $loc2) {
            $m2 = [regex]::Match($loc2.ToString(), "([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")
            if ($m2.Success) {
                $createdOrderForDelete = [pscustomobject]@{ id = $m2.Groups[1].Value; orderDate = $createPayloadForDelete.orderDate }
            }
        }
    }
    if (-not $createdOrderForDelete -or -not $createdOrderForDelete.id) {
        throw "CreateOrder (for delete) did not return an object with an 'id'."
    }
    $deleteOrderId = $createdOrderForDelete.id
    $deleteOrderDate = $createdOrderForDelete.orderDate
    if (-not $deleteOrderDate) { $deleteOrderDate = $createPayloadForDelete.orderDate }

    $deleteUrl = "$BaseUrl/orders/${deleteOrderId}?orderDate=$deleteOrderDate"
    $deleteMs = Measure-HttpMs {
        Invoke-RestMethod -Uri $deleteUrl -Method Delete | Out-Null
    }

    if (-not $SkipBulk) {
        $bulkUrl = "$BaseUrl/orders/bulk"
        $bulkItems = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $BulkCount; $i++) {
            $bulkItems.Add(@{
                locationCode = $locationCode
                productCode  = $productCode
                orderDate    = $tomorrowStr
                quantity     = 1
                submittedBy  = "PerfReport"
            })
        }

        $bulkBody = $bulkItems | ConvertTo-Json -Depth 3
        $bulkMs = Measure-HttpMs {
            Invoke-RestMethod -Uri $bulkUrl -Method Post -ContentType "application/json" -Body $bulkBody
        }
    }
}

$reportLines.Add("| Operation | Scale | Method | Time / Rate |")
$reportLines.Add("|-----------|-------|--------|-------------|")

if ($ingestionSeconds) {
    $rowsLabel = switch ($IngestionPreset) {
        "small" { "100k Rows" }
        "medium" { "1M Rows" }
        "large" { "10M Rows" }
        "xlarge" { "100M Rows" }
        default { "$IngestionPreset" }
    }

    $rateLabel = if ($ingestionRate) { "~$([Math]::Round($ingestionSeconds, 1))s (~$ingestionRate rows/s)" } else { "~$([Math]::Round($ingestionSeconds, 1))s" }
    $reportLines.Add("| **Ingestion** | $rowsLabel | `COPY` (Docker) | $rateLabel |")
} else {
    $reportLines.Add("| **Ingestion** | - | `COPY` (Docker) | skipped |")
}

if ($healthMs) {
    $reportLines.Add("| **Health** | 1 Request | `GET /health` | $([Math]::Round($healthMs, 2)) ms |")
}

if ($locationsSamples.Count -gt 0) {
    $median = Get-Median $locationsSamples
    $p95 = Get-P95 $locationsSamples
    $reportLines.Add("| **List Locations** | All | `GET /locations` | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
}

if ($productsSamples.Count -gt 0) {
    $median = Get-Median $productsSamples
    $p95 = Get-P95 $productsSamples
    $reportLines.Add("| **List Products** | All | `GET /products` | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
}

if ($statsSamples.Count -gt 0) {
    $median = Get-Median $statsSamples
    $p95 = Get-P95 $statsSamples
    $reportLines.Add("| **Order Stats** | Aggregation | `GET /orders/stats` | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
}

if ($createMs) {
    $reportLines.Add("| **Create Order** | 1 Record | Minimal API + EF Core | $([Math]::Round($createMs, 2)) ms |")
}

if ($getSamples.Count -gt 0) {
    $median = Get-Median $getSamples
    $p95 = Get-P95 $getSamples
    $reportLines.Add("| **Get Order** | 1 Record | PK Lookup (+ optional partition pruning) | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
} else {
    $reportLines.Add("| **Get Order** | 1 Record | PK Lookup | skipped |")
}

if ($getNoDateSamples.Count -gt 0) {
    $median = Get-Median $getNoDateSamples
    $p95 = Get-P95 $getNoDateSamples
    $reportLines.Add("| **Get Order (no OrderDate)** | 1 Record | PK Lookup (all partitions) | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
}

if ($listSamples.Count -gt 0) {
    $median = Get-Median $listSamples
    $p95 = Get-P95 $listSamples
    $reportLines.Add("| **List Orders** | Page 1 (20) | FK Index Scan | median $([Math]::Round($median, 2)) ms (p95 $([Math]::Round($p95, 2)) ms) |")
} else {
    $reportLines.Add("| **List Orders** | Page 1 (20) | FK Index Scan | skipped |")
}

if ($updateMs) {
    $reportLines.Add("| **Update Order** | 1 Record | `PUT /orders/{id}` | $([Math]::Round($updateMs, 2)) ms |")
}

if ($deleteMs) {
    $reportLines.Add("| **Delete Order** | 1 Record | `DELETE /orders/{id}` | $([Math]::Round($deleteMs, 2)) ms |")
}

if ($bulkMs) {
    $reportLines.Add("| **Bulk API** | $BulkCount Batch | `BinaryImport` | $([Math]::Round($bulkMs, 2)) ms |")
}

$reportText = ($reportLines -join "`n") + "`n"
$outFile = Join-Path $repoRoot $OutputPath
Set-Content -Path $outFile -Value $reportText -Encoding UTF8

Write-Host "Wrote report to: $outFile"
