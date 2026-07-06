# AI_DECISION_LOG

## #2026-07-06-1 — [오너 결정 필요] BUG-1 수정 방식

**배경**: 사망→재시작 시 `RoomManager.RefreshRoomState()`가 `allMapRooms`에서 플레이어 좌표와 일치하는 방을 못 찾으면 방을 하나도 스폰하지 않음. 유효 목록(프리팹 13 + 씬 오버라이드 3)에 `aut_3`(1,6), `Ending`(1,4), `spr_1 BOSS`(0,1) 누락 확인.

**선택지**:
- **A. 데이터 보수 (정공법, 에디터 필요)**: GameManager 프리팹(또는 FIeld 씬 인스턴스)의 `allMapRooms`에 `aut_3`, `Ending` 추가. `spr_4.asset`의 north(sum_1)와 실제 문 연결(Ending) 불일치도 함께 정리. Ending/sum_1 좌표 중복 (1,4) 처리 방침 결정 필요.
- **B. 코드-온리 하드닝 (에이전트 즉시 실행 가능)**: `RoomManager.Start()`에서 allMapRooms를 이웃(north/south/east/west) 그래프 BFS로 확장 → 누락 방 자동 포함 + 경고 로그. 직렬화 필드/에셋 무변경, ~30줄. 좌표 중복 시 원본 목록 우선.
- **C. A+B 병행 (권장)**: B로 즉시 안전망 확보, A는 에디터 작업 가능할 때 수행.

**결정**: **C (A+B 병행)** — 오너 확정 (2026-07-06).
- B 완료: `7322560` — BFS 안전망 + 매칭 실패 LogError 승격. 조사 중 `spr_1 BOSS` 에셋은 어떤 씬/프리팹에서도 참조되지 않는 고아 데이터로 확인되어 순회 대상에서 자연 제외됨(도달 불가).
- A 대기: 오너 에디터 작업 — allMapRooms에 `aut_3`·`Ending` 추가, `spr_4.asset` north(sum_1) vs 실제 문(Ending) 불일치 정리, Ending/sum_1 좌표 중복 (1,4) 방침 결정.

## #2026-07-06-2 — 브랜치 전략 (에이전트 판단, 지시 준수)

- `bugfix/stabilize-core-loop-20260706` 브랜치를 `hyunseo`에서 생성.
- 승인된 저위험 커밋 `9f9b391`만 체리픽 (→ `e50caf0`).
- `2558003`(스파이크) 통째 체리픽 금지 유지. `refactor/roommanager-split`의 분리 커밋 3개는 가져오지 않음.
- 기존 dirty 파일(.gitignore, MainCanvas.prefab, FIeld.unity, CombatInputHelper.cs, ProjectSettings.asset)은 오너 소유 미커밋 변경으로 간주, 커밋에 포함하지 않음.

## #2026-07-06-3 — 진단 관련 관찰 (오너 참고)

- 작업 트리의 미커밋 `FIeld.unity` 변경에 RoomManager `debugLogs: 0` 설정이 포함되어 있음 → BUG-1 진단에 필요한 `No RoomData matched` 경고가 콘솔에 안 뜸. **재현 테스트 전에 에디터에서 debugLogs를 다시 켜는 것을 권장** (씬 파일은 에이전트가 수정하지 않음).
