# FABLE_HANDOFF

> 다음 세션의 AI 에이전트가 이 파일부터 읽을 것. (갱신: 2026-07-06)

## 현재 모드
**BUG-STABILIZATION-FIRST.** 일반/코스메틱 리팩토링 전면 중단. RoomManager 분리 작업도 버그 진단/수정에 직접 필요하지 않으면 중단. 상세 규칙: `AI_GUARDRAILS.md`.

## 브랜치 상태
- 작업 브랜치: `bugfix/stabilize-core-loop-20260706` = `hyunseo` + `9f9b391` 체리픽(`e50caf0`).
- `refactor/roommanager-split`(hyunseo + 분리 커밋 3개)은 그대로 보존, 추가 진행 금지.
- `spike/refactor-fable-20260706`의 `2558003`은 통째 체리픽 금지.
- 오너의 미커밋 dirty 파일 5개는 건드리지도 커밋하지도 말 것 (목록: AI_GUARDRAILS.md).

## 버그 현황 (상세: AI_BUG_REGISTRY.md)
- **BUG-1 (근본 원인 확인, C안 진행 중)**: `allMapRooms`에 `aut_3`/`Ending` 누락 → 그 방에서 사망/재시작 시 `RefreshRoomState()` 좌표 매칭 실패 → 방 0개 스폰. **B(코드 안전망) 완료 = 커밋 `7322560`** (BFS 자동 보강 + LogError 승격). A(에디터 데이터 보수)와 재현 검증은 오너 대기 (T-109/T-110). `spr_1 BOSS` 에셋은 미참조 고아 데이터로 확인.
- BUG-2: spr_4(ThreeWitch) 북쪽 문 미차단 — MapNode isTrigger 토글 구조는 확인, 콜라이더 실측 필요.
- BUG-3: 문이 평상시 isTrigger=true라 몬스터를 물리적으로 못 막음 (차단 로직 부재) — 정책 결정 필요.
- BUG-4/5/6: 미조사 (순서와 의심 파일은 레지스트리 참조).

## 핵심 지식 (재조사 방지)
- 방 데이터: `Assets/Scripts/Field/*.asset` 18개. 좌표 중복: spr_1↔spr_1 BOSS (0,1), Ending↔sum_1 (1,4).
- `allMapRooms` 유효 목록 = GameManager.prefab 13개 + FIeld.unity 오버라이드 [13]=sum_2, [14]=sum_1, [15]=sum_3(중복).
- `spr_4.prefab` 북쪽 문(MapNode_4toe, BoxCollider2D 6×1, local y=+8.5)의 nextRoom은 `Ending`인데 `spr_4.asset`의 north는 `sum_1` — 데이터 불일치.
- `overrideDistance`는 프로젝트 전체에서 전부 0 → 방 배치는 그리드 정합.
- 평상시 방 이동은 RoomData 이웃 참조 기반이라 allMapRooms 누락이 안 드러남. 재시작 경로(`RefreshRoomState` → `GetRoomDataByCoord`)에서만 터짐.
- 미커밋 FIeld.unity 변경에 `debugLogs: 0` 포함 → 콘솔 경고가 꺼져 있음 (오너에게 켜달라고 요청할 것).

## 다음 행동
1. 오너의 T-110 재현 검증 결과 확인 (aut_3 사망→재시작).
2. BUG-2: 오너 에디터 테스트 결과 대기 or MapNode 임시 진단 로그 제안.
3. T-106/107/108 read-only 감사 순차 진행.
