from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_hunter_impact_samples_use_particle_age_and_leave_production_camera_untouched():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    impact = source.split('string CaptureHunterOffscreenImpact(', 1)[1].split('string MaterialSnapshot(', 1)[0]
    render_hook = source.split('void CaptureSuperSpellRender(', 1)[1].split('void CaptureSuperSpellDraw(', 1)[0]
    pump = source.split('void PumpHunterDiagnostics()', 1)[1].split('void WriteHunterSampleSummary()', 1)[0]

    assert 'HunterImpactSampleAges={.25f,.45f,.65f}' in source
    assert 'const int HunterImpactSampleToleranceMs=50' in source
    assert 'particle.name=="Bang"' in impact
    assert 'if(bang.particleCount==0)' in impact
    assert 'bang.time*1000>Mathf.RoundToInt(HunterImpactSampleAges[nextHunterImpactSample]*1000)+HunterImpactSampleToleranceMs' in impact
    assert 'Mathf.Abs(bang.time*1000-targetMs)>HunterImpactSampleToleranceMs' in impact
    assert 'first source Repaint after target tolerance window' in impact
    assert 'cloneAge=impact.PlaybackTime,particleAge=bang.time' in impact
    assert 'targetAgeErrorMs=bang.time*1000-targetMs' in impact
    assert 'RequestHunterPixelReadback(effect.Texture' in impact
    assert 'stage=CaptureHunterOffscreenImpact(eventKey,spell)' in render_hook
    assert 'row.pixels=pixels;row.done=true' in impact
    assert 'pendingHunterImpacts.Any(item=>!item.done)' in pump
    assert 'CalculatePixelStats(stats)' in pump
    assert 'impact.image),texture.EncodeToPNG()' in pump
    for forbidden in ('camera.Render(', '.orthographicSize=', 'ResetProjectionMatrix', 'effect.Render(', 'ReadPixels', 'Simulate('):
        assert forbidden not in impact
    assert 'cast.Flow.Time-cast.Flow.FirstArrivalTime.Value>=.95f' not in source


def test_hunter_screen_png_normalizes_readback_rows_before_roi_extraction():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    screen = source.split('void ProcessHunterScreen(', 1)[1].split('void PumpHunterDiagnostics(', 1)[0]
    assert 'rowsStartAtTop=SystemInfo.graphicsUVStartsAtTop' in source
    assert 'readbackRowsStartAtTop=capture.rowsStartAtTop,pngRowsFlipped=capture.rowsStartAtTop' in screen
    assert 'Buffer.BlockCopy(capture.rgba,y*stride,pngPixels,(capture.height-1-y)*stride,stride)' in screen
    assert screen.index('pixels.LoadRawTextureData(pngPixels)') < screen.index('pixels.GetPixels32()')
    assert 'camera.Render(' not in screen and 'Graphics.Blit(' not in screen


def test_hunter_impact_summary_reports_missing_late_ages_without_faking_complete_capture():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    summary = source.split('bool WriteHunterImpactSampleSummary()', 1)[1].split('void WriteHunterSampleSummary()', 1)[0]
    assert 'sample.done&&sample.pixels!=null&&sample.pixels.readbackSucceeded' in summary
    assert 'Mathf.Abs(sample.targetAgeErrorMs)<=HunterImpactSampleToleranceMs' in summary
    assert 'var missing=expected.Except(captured).ToArray()' in summary
    assert 'complete=missing.Length==0&&captured.Length==expected.Length&&hunterImpactSampleMisses.Count==0' in summary
    assert 'lastUnavailableReason=missing.Length==0?"":hunterImpactLastUnavailableReason' in summary
    assert 'hunter-impact-sample-summary.json' in summary
    assert 'hunterTrailSamplingComplete=' in source and 'hunterImpactSamplingComplete=' in source


def test_hunter_impact_draw_does_not_inspect_released_missile_objects():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    draw = source.split('void CaptureSuperSpellDraw(', 1)[1].split('bool TryTrailScreenRoi(', 1)[0]
    flight = source.split('void CaptureFlightDiagnostic(', 1)[1].split('bool CaptureReady(', 1)[0]
    # The original impact stages extend beyond missile cleanup. Retained managed
    # flight records can still reference a destroyed Unity root and camera.
    guard = 'if(row.sampleIndex<0&&!row.stage.StartsWith("impact-particle-",StringComparison.Ordinal))'
    assert draw.index(guard) < draw.index('var flights=') < draw.index('CaptureFlightDiagnostic(')
    assert draw.index('StartCoroutine(CaptureFinalHunterScreen(row))') > draw.index(guard)
    assert flight.index('if(effect.Root==null||camera==null)') < flight.index('var visuals=')
    assert 'unavailable after release' in flight


def test_hunter_trail_sampling_is_nonblocking_and_fail_closed():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    player = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectPlayer.cs').read_text(encoding='utf-8')
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    flight = (ROOT / 'UnityClient/Assets/Scripts/NativeMissileFlight.cs').read_text(encoding='utf-8')
    effect = (ROOT / 'UnityClient/Assets/Scripts/NativeEffectInstance.cs').read_text(encoding='utf-8')

    render_hook = source.split('void CaptureSuperSpellRender(', 1)[1].split('void CaptureSuperSpellDraw(', 1)[0]
    screen_hook = source.split('System.Collections.IEnumerator CaptureFinalHunterScreen(', 1)[1].split(
        'void ProcessHunterScreen(', 1
    )[0]

    assert 'HunterArrivalSampleAges={0f,.1f,.2f}' in source
    assert 'spell.Flow.Time-spell.Flow.FirstArrivalTime.Value' in source
    assert 'sampleIndex=nextHunterArrivalSample++' in source
    assert 'role!="driver"' in render_hook
    assert 'RequestHunterPixelReadback' in render_hook
    assert 'ReadPixels' not in render_hook
    assert 'AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat' in source
    assert 'sourceArrivalAge=sourceArrivalAge' in source
    assert 'callbackFrame=Time.frameCount' in source
    assert 'callbackArrivalAge=HunterArrivalAge(spell)' in source

    assert 'trailEmitting=trail!=null&&trail.emitting' in source
    assert 'trailPositionCount=trail==null?0:trail.positionCount' in source
    assert 'trailWidth=trail==null?0:trail.widthMultiplier' in source
    assert 'trailMeshVertices=meshVertices,trailMeshTriangles=meshTriangles' in source
    assert 'trailMeshBaked=trailMeshBaked' in source
    assert 'trail.GetPositions(worldPoints)' in source
    assert 'HunterRuntimeMaterialSnapshot.Capture(material' in source

    # RenderAffine advances source state and Trail before the production RT and GUI diagnostics.
    assert player.index('var emission=spell.RenderAffine(age+spell.CastOffset)') < player.index(
        'SourceSpellRenderDiagnostic?.Invoke(key,spell,emission,spell.Effect.Transmission)'
    ) < player.index('bool passes=RenderAffine(screenRect,emission,spell.Effect.Transmission') < player.index(
        'SourceSpellDrawDiagnostic?.Invoke(key,spell,screenRect'
    )
    render_affine = playback.split('public RenderTexture RenderAffine(float age)', 1)[1].split('public void Dispose()', 1)[0]
    # Source-only roots can render their own capture before the shared missile path.
    # Assert the Hunter path's ordering without depending on a one-line method.
    assert render_affine.index('Seek(age)') < render_affine.index('return Effect.RenderTogether(')
    assert 'clock.Tick(time);RequireActive();' in (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpell.cs').read_text(encoding='utf-8')
    assert 'clone.Seek(next);time=next;' in flight
    assert 'foreach(var trail in trails)trail.Sample(age);' in effect

    assert 'yield return new WaitForEndOfFrame();' in screen_hook
    assert 'ScreenCapture.CaptureScreenshotIntoRenderTexture(target)' in screen_hook
    assert 'AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32' in screen_hook
    assert 'ReadPixels' not in screen_hook
    assert 'requestFrame==repaint.frame' in source
    assert 'captureFrame=capture.requestFrame' in source
    assert 'readbackCallbackFrame=capture.callbackFrame' in source
    assert 'arrivalAge=repaint.arrivalAge,callbackArrivalAge=capture.callbackArrivalAge' in source

    assert 'const int HunterSampleToleranceMs=40' in source
    assert 'withinTargetAgeTolerance=Mathf.Abs(diagnostic.targetAgeErrorMs)<=HunterSampleToleranceMs' in source
    assert 'withinTargetAgeTolerance=Mathf.Abs(ageError)<=HunterSampleToleranceMs' in source
    assert 'summary.samples.All(sample=>sample.withinTargetAgeTolerance&&sample.guiSameFrame&&sample.guiPassesBound&&sample.screenSameFrame&&' in source
    assert 'sample.renderReadbackSucceeded&&sample.screenReadbackSucceeded' in source
    assert 'hunterDiagnosticDeadline=Time.realtimeSinceStartup+10f' in source

    finish = source.split('void Finish(string result,int exit)', 1)[1].split('void PublishReportResult(', 1)[0]
    pump = source.split('void PumpHunterDiagnostics()', 1)[1].split('void WriteHunterSampleSummary()', 1)[0]
    assert 'if(!(Hunter&&role=="driver"))PublishReportResult(result)' in finish
    assert 'PublishReportResult(finishResult);hunterDiagnosticFinished=true;' in pump


def test_hunter_age_windows_record_candidates_and_skip_heavy_sample_probes():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    render_hook = source.split('void CaptureSuperSpellRender(', 1)[1].split('void CaptureSuperSpellDraw(', 1)[0]
    flight_probe = source.split('void CaptureFlightDiagnostic(', 1)[1].split('bool CaptureReady(', 1)[0]
    summary = source.split('void WriteHunterSampleSummary()', 1)[1].split('void CaptureFlightDiagnostic(', 1)[0]
    hunter_tick = source.split('if(Hunter&&cast!=null)', 1)[1].split('if(tap!=null)', 1)[0]

    assert 'const int MaxHunterAgeCandidates=512' in source
    assert 'RecordHunterAgeCandidate(eventKey,spell,arrivalAge,targetAgeMsForWindow)' in render_hook
    assert 'firstObservedAge=arrivalAge' in source
    assert 'reason="first Repaint arrived after the target tolerance window"' in source
    assert 'ageCandidates=hunterAgeCandidates.ToArray()' in summary
    assert 'sampleWindowMisses=hunterSampleWindowMisses.ToArray()' in summary
    assert 'reason="no Repaint candidate was observed in the target tolerance window before finalization"' in summary
    assert 'missingTargetAgesMs=missing' in summary
    assert 'summary.droppedAgeCandidates==0' in summary

    # A selected sample is inside its unchanged +/-40 ms band. Overshoot is recorded as a miss.
    assert 'if(arrivalAge>upperAge)' in render_hook
    assert 'RecordHunterSampleWindowMiss(eventKey,nextHunterArrivalSample,arrivalAge)' in render_hook
    assert 'else if(arrivalAge>=lowerAge)' in render_hook
    assert 'RequestHunterPixelReadback(emission,arrivalAge,spell' in render_hook
    assert 'RequestHunterPixelReadback(transmission,arrivalAge,spell' in render_hook
    assert 'instance.Render(' not in render_hook
    assert 'trail.emitting=true' not in render_hook
    assert 'if(trail!=null&&sampleIndex<0)' in flight_probe
    assert 'trail.BakeMesh(baked,camera,true)' in flight_probe
    assert 'CaptureFlightDiagnostic("arrival",flight)' not in hunter_tick


def test_hunter_target_frame_trail_mesh_bake_follows_screen_request_and_fails_closed():
    source = (ROOT / 'UnityClient/Assets/Scripts/FireblastReplay.cs').read_text(encoding='utf-8')
    install = source.split('static void Install()', 1)[1].split('void ', 1)[0]
    screen_hook = source.split('System.Collections.IEnumerator CaptureFinalHunterScreen(', 1)[1].split(
        'HunterTrailMeshDiagnostic CaptureHunterTrailMesh(', 1
    )[0]
    mesh_capture = source.split('HunterTrailMeshDiagnostic CaptureHunterTrailMesh(', 1)[1].split(
        'void ProcessHunterScreen(', 1
    )[0]
    render_hook = source.split('void CaptureSuperSpellRender(', 1)[1].split('void CaptureSuperSpellDraw(', 1)[0]
    summary = source.split('void WriteHunterSampleSummary()', 1)[1].split('void CaptureFlightDiagnostic(', 1)[0]

    # The diagnostic is installed only for an explicit replay and only bakes selected H05 samples.
    assert 'if(role=="")return;' in install
    assert 'if(!Hunter||repaint==null' in screen_hook
    assert 'if(repaint.sampleIndex>=0)' in screen_hook
    assert 'trail.BakeMesh' not in render_hook
    assert screen_hook.index('yield return new WaitForEndOfFrame();') < screen_hook.index(
        'AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32'
    ) < screen_hook.index('capture.readbackRequested=true;') < screen_hook.index('CaptureHunterTrailMesh(repaint,capture,sample)')

    assert 'sample.eventKey!=repaint.eventKey' in mesh_capture
    assert 'if(!capture.readbackRequested)' in mesh_capture
    assert 'capture.requestFrame!=repaint.frame||Time.frameCount!=repaint.frame' in mesh_capture
    assert 'diagnostic.bakeFrame=Time.frameCount' in mesh_capture
    assert 'trail.BakeMesh(baked,camera,true)' in mesh_capture
    assert 'diagnostic.triangleCount=baked.triangles.Length/3' in mesh_capture
    assert 'diagnostic.vertexAlphaMin=colors.Min(color=>color.a)' in mesh_capture
    assert 'diagnostic.vertexAlphaMax=colors.Max(color=>color.a)' in mesh_capture
    assert 'meshBoundsMin=bounds.min' in mesh_capture and 'meshBoundsMax=bounds.max' in mesh_capture
    assert 'sample.trailMeshSameFrame=meshDiagnostic.sameRepaintFrame' in screen_hook
    assert 'sample.trailMeshDiagnosticWritten=true' in screen_hook
    assert 'sample.trailMeshSameFrame&&sample.trailMeshBaked&&sample.trailMeshDiagnosticWritten' in summary


def test_hunter_final_draw_replays_captured_trail_without_resampling_it():
    source = (ROOT / 'UnityClient/Assets/Editor/HunterFinalDrawDiagnostic.cs').read_text(encoding='utf-8')
    run = source.split('public static void Run()', 1)[1]

    assert 'DrawProgramNodes' in source
    assert 'recordedTrail.trailWorldPoints' in run
    assert '!recordedArrow.enabled&&!recordedGlow.enabled' in run
    assert 'new NativeAffineCapture(1024,576)' in run
    assert 'affine.Render(effect.PreviewCamera' in run
    assert 'compositor.RenderAffine(new Rect(' in run
    assert 'effect.RenderTogether(' not in run


def test_hunter_missile_source_uses_the_live_hero_power_position():
    playback = (ROOT / 'UnityClient/Assets/Scripts/NativeSuperSpellPlayback.cs').read_text(encoding='utf-8')
    checks = (ROOT / 'UnityClient/Assets/Editor/PresentationChecks.cs').read_text(encoding='utf-8')

    resolver = playback.split('public static Vector2? ResolvePresentationPosition', 1)[1].split(
        'public static int Controller', 1
    )[0]
    assert 'cardId!="HERO_05bp"' in resolver
    assert 'NativeSuperSpellBoard.ResolvePresentationPosition(card,2,2,locate)==sourcePosition' in checks
    assert 'ResolvePresentationPosition("HERO_05bp",2,9,locate)==null' in checks
