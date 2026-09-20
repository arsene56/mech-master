param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$bomPath = Join-Path $RepositoryRoot 'Assets\StreamingAssets\MechanicalCatalog\bicycle_engineering.json'
$modelPath = Join-Path $RepositoryRoot 'Assets\StreamingAssets\MechanicalCatalog\bicycle_model_manifest.json'
$outputPath = Join-Path $RepositoryRoot 'Assets\Resources\MechanicalCatalog\BicycleInteractionCatalog.json'
$bom = Get-Content -Raw -LiteralPath $bomPath | ConvertFrom-Json
$model = Get-Content -Raw -LiteralPath $modelPath | ConvertFrom-Json

$assemblyOrder = @(
    'controls_cables', 'saddle_seatpost', 'pedals', 'brake_front', 'brake_rear',
    'chain', 'front_derailleur', 'rear_derailleur', 'wheel_front', 'wheel_rear',
    'crank_bottom_bracket', 'cockpit_headset', 'fork', 'frame'
)

$assemblyById = @{}
$componentsByAssembly = @{}
foreach ($assembly in $bom.assemblies) {
    $assemblyById[$assembly.id] = $assembly
    if ($assembly.components) {
        $componentsByAssembly[$assembly.id] = @($assembly.components)
    }
}

$objectsByAssembly = @{}
$objectsByComponent = @{}
foreach ($part in $model.parts) {
    if (-not $objectsByAssembly.ContainsKey($part.assemblyId)) {
        $objectsByAssembly[$part.assemblyId] = [System.Collections.Generic.List[string]]::new()
    }
    $objectsByAssembly[$part.assemblyId].Add($part.object)
    $segments = $part.partId.Split('.')
    $componentKey = ($segments[0..2] -join '.')
    if (-not $objectsByComponent.ContainsKey($componentKey)) {
        $objectsByComponent[$componentKey] = [System.Collections.Generic.List[string]]::new()
    }
    $objectsByComponent[$componentKey].Add($part.object)
}

function Get-FunctionText([string]$id, [string]$name, [string]$assemblyName) {
    switch -Regex ($id) {
        'tire' { return '轮胎与地面接触，提供抓地力并吸收细小震动。' }
        'inner_tube|valve' { return '它负责保存和调节轮胎内的空气压力。' }
        '^rim$|rim_tape' { return '轮圈支撑轮胎，并把路面载荷传给辐条系统。' }
        'spoke|nipple' { return '它通过张力连接轮圈与花鼓，帮助轮组保持圆度和侧向稳定。' }
        'hub|freehub|pawl' { return '它位于车轮中心，支撑旋转并把驱动力或制动力传给轮组。' }
        'rotor' { return '碟片随车轮旋转，来令片夹紧它时把运动能量转化为热。' }
        'brake_pad|pad_' { return '它直接接触碟片产生摩擦，是会逐渐磨损的制动零件。' }
        'piston' { return '活塞把液压或气压转换为直线推力。' }
        'hose|olive|connector|compression_nut' { return '它连接液压系统并保持油路密封，使压力可以可靠传递。' }
        'lever|reservoir|diaphragm' { return '它把手指动作转换为液压，并为刹车油留出补偿空间。' }
        'caliper' { return '卡钳容纳活塞和来令片，并从两侧夹紧碟片。' }
        'chainring|cassette_sprocket' { return '不同齿数组合会改变踩踏力量与车轮转速之间的比例。' }
        'chain' { return '链条把牙盘的转动传递到后轮飞轮。' }
        'derailleur|cage|jockey|limit_screw|b_tension' { return '它引导链条移动或保持张力，让链条准确落在目标齿片上。' }
        'crank|pedal' { return '它承接双脚的踩踏力量，并把力量传给牙盘。' }
        'bottom_bracket' { return '中轴组件支撑曲柄旋转，同时把踩踏载荷传给车架。' }
        'bearing|bushing' { return '它支撑运动零件并减小摩擦，同时帮助零件保持正确位置。' }
        'seal|wiper|foam' { return '它阻挡灰尘和水分，并帮助润滑介质留在机构内部。' }
        'fork|stanchion|damper|air_|rebound|compression' { return '它属于避震前叉系统，用弹性元件吸收冲击、用阻尼控制运动速度。' }
        'handlebar|stem|headset|crown_race|top_cap|spacer' { return '它属于转向系统，把骑手的转向动作传给前叉和前轮。' }
        'saddle|seatpost' { return '它支撑骑手，并通过座管调节乘坐高度和位置。' }
        'shifter|shift_|cable|housing|ferrule' { return '它把手指的变速指令传给拨链器。' }
        'bolt|screw|nut|clip|pin|washer|cap|clamp|collar' { return '它负责定位、连接或防松，使相邻零件保持正确装配关系。' }
        'frame|hanger|protector|guide' { return '它属于车架结构或车架小件，负责承载、定位或保护其他系统。' }
        default { return "$name 是$assemblyName 中的组成零件，负责连接、支撑、传力或保护。" }
    }
}

function New-Step(
    [string]$id,
    [string]$displayName,
    [string]$tool,
    [string]$summary,
    [string]$mechanism,
    [string]$advanced,
    [string]$assemblyId,
    [string]$componentId,
    [string[]]$objectNames
) {
    return [ordered]@{
        id = $id
        displayName = $displayName
        tool = $tool
        simpleSummary = $summary
        mechanism = $mechanism
        advancedNote = $advanced
        assemblyId = $assemblyId
        componentId = $componentId
        objectNames = @($objectNames)
    }
}

function Limit-Text([string]$text, [int]$maxLength = 50) {
    $value = if ($null -eq $text) { '' } else { $text }
    $value = ($value -replace '\s+', ' ').Trim()
    if ($value.Length -le $maxLength) {
        return $value
    }

    return $value.Substring(0, $maxLength - 1).TrimEnd() + '…'
}

$groupCounts = @{
    Simple = @{
        frame = 1; cockpit_headset = 1; fork = 1; wheel_front = 1; wheel_rear = 2
        brake_front = 1; brake_rear = 1; crank_bottom_bracket = 1; front_derailleur = 1
        rear_derailleur = 1; chain = 1; pedals = 1; saddle_seatpost = 1; controls_cables = 1
    }
    Standard = @{
        frame = 2; cockpit_headset = 2; fork = 3; wheel_front = 2; wheel_rear = 3
        brake_front = 3; brake_rear = 3; crank_bottom_bracket = 2; front_derailleur = 2
        rear_derailleur = 2; chain = 1; pedals = 2; saddle_seatpost = 2; controls_cables = 1
    }
    Advanced = @{
        frame = 2; cockpit_headset = 3; fork = 4; wheel_front = 4; wheel_rear = 5
        brake_front = 4; brake_rear = 4; crank_bottom_bracket = 4; front_derailleur = 3
        rear_derailleur = 3; chain = 2; pedals = 2; saddle_seatpost = 2; controls_cables = 3
    }
}

$plansByDifficulty = @{}
foreach ($difficulty in @('Simple', 'Standard', 'Advanced')) {
    $steps = [System.Collections.Generic.List[object]]::new()
    $difficultyId = $difficulty.ToLowerInvariant()
    foreach ($assemblyId in $assemblyOrder) {
        $assembly = $assemblyById[$assemblyId]
        $sourceId = if ($assembly.inheritComponentsFrom) { $assembly.inheritComponentsFrom } else { $assemblyId }
        $components = @($componentsByAssembly[$sourceId])
        $groupCount = [int]$groupCounts[$difficulty][$assemblyId]
        if ($groupCount -lt 1 -or $groupCount -gt $components.Count) {
            throw "$difficulty / $assemblyId 的分组数 $groupCount 无效（组件数 $($components.Count)）"
        }

        for ($groupIndex = 0; $groupIndex -lt $groupCount; $groupIndex++) {
            $start = [int][Math]::Floor($groupIndex * $components.Count / $groupCount)
            $end = [int][Math]::Floor(($groupIndex + 1) * $components.Count / $groupCount) - 1
            $groupComponents = @($components[$start..$end])
            $objectNames = [System.Collections.Generic.List[string]]::new()
            foreach ($component in $groupComponents) {
                $componentKey = "bike.$assemblyId.$($component.id)"
                foreach ($objectName in @($objectsByComponent[$componentKey])) {
                    $objectNames.Add($objectName)
                }
            }

            $componentNames = @($groupComponents | ForEach-Object { $_.name })
            $displayName = if ($groupCount -eq 1) {
                $assembly.name
            } elseif ($componentNames.Count -eq 1) {
                "$($assembly.name) · $($componentNames[0])"
            } else {
                "$($assembly.name) · $($componentNames[0])等"
            }
            $functionTexts = @($groupComponents | ForEach-Object {
                Get-FunctionText $_.id $_.name $assembly.name
            } | Select-Object -Unique)
            $componentIds = @($groupComponents | ForEach-Object { $_.id })
            $suffix = ($groupIndex + 1).ToString('00')
            $summary = Limit-Text "这是$($assembly.name)中的$($componentNames[0])相关部件。"
            $mechanism = Limit-Text $(if ($functionTexts.Count -gt 0) { $functionTexts[0] } else { '它负责连接、支撑或传递机械力量。' })
            $advanced = Limit-Text "本组含$($componentNames.Count)类、$($objectNames.Count)个实体，协同完成$($assembly.name)的功能。"
            $steps.Add((New-Step "plan.$difficultyId.$assemblyId.group.$suffix" $displayName 'Hand' `
                $summary `
                $mechanism `
                $advanced `
                $assemblyId ($componentIds -join '+') @($objectNames | Sort-Object -Unique)))
        }
    }
    $plansByDifficulty[$difficulty] = $steps
}

$simple = $plansByDifficulty['Simple']
$standard = $plansByDifficulty['Standard']
$advanced = $plansByDifficulty['Advanced']

$payload = [ordered]@{
    schemaVersion = 1
    moduleId = $bom.moduleId
    plans = @(
        [ordered]@{ difficulty = 'Simple'; steps = @($simple) },
        [ordered]@{ difficulty = 'Standard'; steps = @($standard) },
        [ordered]@{ difficulty = 'Advanced'; steps = @($advanced) }
    )
}

$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[System.IO.File]::WriteAllText($outputPath, ($payload | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $outputPath"
Write-Output "Steps: Simple=$($simple.Count), Standard=$($standard.Count), Advanced=$($advanced.Count)"
