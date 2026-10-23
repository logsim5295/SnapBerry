# 버전 태그 공개 릴리스 구현 계획

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** `v1.2.3` 형식의 태그를 푸시하면 Windows x64와 macOS arm64/x64 설치 파일을 공개 릴리스에 첨부한다.

**Architecture:** 별도 `release.yml`에서 태그 검증, Windows와 macOS 빌드, 전체 산출물 검증, 릴리스 공개를 순서대로 수행한다. 기존 Portable 난독화와 macOS DMG 생성 스크립트를 재사용한다. 빌드 서버의 작업 사본에서만 앱과 Info.plist의 버전을 태그에 맞춘다.

**Tech Stack:** GitHub Actions, .NET 10, PowerShell, Python 표준 라이브러리, GitHub CLI.

**Spec:** 이 대화의 태그 기반 Windows·macOS 자동 릴리스 요구. 기존 CI에 배포를 결합하면 일반 브랜치 검증에도 배포 조건이 섞이므로 별도 워크플로를 선택한다.

## Global Constraints

- 앱을 실행하거나 화면·클립보드·전역 단축키를 조작하는 테스트는 추가하지 않는다.
- 일반 Release와 로컬 Git LFS EXE를 유지한다. 이번 변경은 CI·배포 도구·문서다.
- 태그는 `vMAJOR.MINOR.PATCH` 형식이며 .NET 버전 범위 안의 숫자만 받는다.
- EXE, Windows ZIP, DMG 두 개, 안내 문서 세 개, `SHA256SUMS.txt`를 배포한다.
- 플랫폼 하나라도 실패하거나 파일이 누락·변조되면 공개하지 않는다.
- 공개된 릴리스는 재실행 시 유지한다. 미완성 초안은 재시도할 수 있다.
- 수동 실행은 빌드·패키지 검증만 하며 공개 릴리스를 생성하지 않는다.
- 릴리스 작업만 `contents: write`를 사용하며 별도 PAT는 요구하지 않는다.

## Review Focus

- 잘못된 태그: 파일을 변경하기 전에 거부한다.
- 다음 버전: 두 앱과 macOS 번들 버전에 모두 반영한다.
- 누락된 CPU·불필요한 PDB: 전체 파일 집합을 검사해 공개를 차단한다.
- 업로드 중 실패: 초안으로 남기고 파일 크기·해시를 모두 확인한 뒤 공개한다.
- 재실행: 공개된 파일을 덮어쓰지 않는다.

## Task 1: 배포 도구와 동작 테스트

**Files:** `build/release.py`, `build/tests/test_release.py`.

**Interfaces:** `version TAG`, `prepare TAG --root ROOT`, `windows --version VERSION --source DIR --output DIR`, `manifest --version VERSION --directory DIR`, `verify-upload --version VERSION --directory DIR --metadata FILE`.

- [x] CLI 테스트를 먼저 작성하고 정상 경로가 구현 부재로 실패함을 확인한다.
- [x] 태그 검증, 작업 사본 버전 설정, Windows ZIP, 전체 체크섬과 원격 업로드 검사를 구현한다.
- [x] Python 배포·패키지 테스트 전체를 실행한다.

## Task 2: 워크플로와 사용 안내

**Files:** `.github/workflows/release.yml`, `.github/workflows/macos.yml`, `README.md`.

- [x] 태그 검증·세 플랫폼 빌드·산출물 집계·초안 업로드·공개 작업을 연결한다.
- [x] 기존 macOS 브랜치 검증이 태그에서도 중복 실행되지 않도록 범위를 명시한다.
- [x] 태그 푸시·수동 검증·재실행·버전 정책을 README에 설명한다.
- [x] actionlint, Python 테스트, Release 빌드와 독립 리뷰로 검증한다.
- [ ] 한국어 커밋으로 main에 반영하고 수동 검증을 실행한다. 실제 새 버전 태그는 만들지 않는다.
