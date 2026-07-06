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
- **BUG-1: 해결됨 (오너 검증 2026-07-06)** — 코드 안전망 `7322560` + 오너 데이터 보수(allMapRooms, spr_4 문→sum_1).
- **BUG-2: 수정 완료** `c878b06` (오너 승인) — SetBlockades에서 MapNode 비활성화 제거. 오너 플레이테스트 검증 대기.
- BUG-3: 문이 평상시 isTrigger=true라 몬스터 차단 장치 부재 — 정책 결정 대기 (T-105).
- **BUG-4: 원인 확정, 결정 대기** — `EnemyCombat.ApplySelfKnockback`(플레이어 접촉 시 2유닛)과 `EnemyStatusController.ApplyKnockback`(포션)이 `rb.MovePosition` 순간이동, 충돌검사 없음. 근접 공격 자체는 넉백 안 줌. 수정안: rb.Cast 클램프 (결정 카드 #2026-07-06-6).
- **BUG-5: 코드측 원인 후보 확정** — BossProjectile 태그 게이트 때문에 정밀 히트박스(0.6배, Untagged 자식) 대신 본체 캡슐 전체가 판정. 프리팹 실측은 에디터 필요 (결정 카드 #2026-07-06-7).
- BUG-6: 후보 3건(셀프넉백 핑퐁 / detectionPoint-transform 기준 불일치 상태 떨림 / 벽회피 부재). 오너의 증상 구체화 대기.
- CombatInputHelper.cs dirty 변경은 주석 정리로 확인(BUG-4 무관).

## 기획 대기 (오너 확인 — AI가 건드리지 말 것)
- 좌표 혼재(동일 좌표 복수 방) = 조건부 스토리 연결 예정, 기획 미완. 임의 정리 금지.
- Ending 방 = 알파테스트 레거시 (spr_4 연결은 오너가 sum_1로 이미 수정).
- aut_3 = 현재 실제 진입 불가 (spr_6 위쪽은 spr_7).

## 핵심 지식 (재조사 방지)
- 방 데이터: `Assets/Scripts/Field/*.asset` 18개. 좌표 중복: spr_1↔spr_1 BOSS (0,1), Ending↔sum_1 (1,4).
- `allMapRooms` 유효 목록 = GameManager.prefab 13개 + FIeld.unity 오버라이드 [13]=sum_2, [14]=sum_1, [15]=sum_3(중복).
- `spr_4.prefab` 북쪽 문(MapNode_4toe, BoxCollider2D 6×1, local y=+8.5)의 nextRoom은 `Ending`인데 `spr_4.asset`의 north는 `sum_1` — 데이터 불일치.
- `overrideDistance`는 프로젝트 전체에서 전부 0 → 방 배치는 그리드 정합.
- 평상시 방 이동은 RoomData 이웃 참조 기반이라 allMapRooms 누락이 안 드러남. 재시작 경로(`RefreshRoomState` → `GetRoomDataByCoord`)에서만 터짐.
- 미커밋 FIeld.unity 변경에 `debugLogs: 0` 포함 → 콘솔 경고가 꺼져 있음 (오너에게 켜달라고 요청할 것).

## 다음 행동
1. 오너 BUG-2 플레이테스트 결과 확인.
2. 오너 결정 대기: #2026-07-06-6(BUG-4 넉백 클램프), #2026-07-06-7(BUG-5 판정 기준), T-105(BUG-3 문 차단 정책), BUG-6 증상 구체화.
3. 승인된 항목부터 한 커밋 = 한 레이어로 수정.
