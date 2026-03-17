// Full E2E Integration Test Script
const API = 'http://localhost:5228/api/v1';
const PHONE = '998901234567';
const OTP_CODE = '111111';

let TOKEN = '';
let REFRESH_TOKEN = '';
let results = [];
let examSessionId = '';
let ticketExamId = '';
let marathonId = '';
let questionId = '';

async function req(method, path, body, extraHeaders = {}) {
  const headers = { 'Content-Type': 'application/json', ...extraHeaders };
  if (TOKEN) headers['Authorization'] = `Bearer ${TOKEN}`;
  const opts = { method, headers };
  if (body) opts.body = typeof body === 'string' ? body : JSON.stringify(body);
  const res = await fetch(`${API}${path}`, opts);
  const text = await res.text();
  try { return { status: res.status, data: JSON.parse(text) }; }
  catch { return { status: res.status, data: text }; }
}

function test(name, condition, detail = '') {
  const status = condition ? '✅' : '❌';
  results.push({ name, status, detail });
  console.log(`${status} ${name}${detail ? ' — ' + detail : ''}`);
  return condition;
}

// Helper: get display name from LocalizedText object
function lt(obj) {
  if (!obj) return '(null)';
  if (typeof obj === 'string') return obj;
  return obj.uzLatin || obj.uz || obj.ru || JSON.stringify(obj);
}

async function run() {
  console.log('🚀 FULL E2E INTEGRATION TEST\n');

  // ========== AUTH FLOW ==========
  console.log('── AUTH ──');

  // 1. Send OTP
  let r = await req('POST', '/auth/otp/send', { phoneNumber: PHONE });
  test('POST /auth/otp/send', r.data.success === true, `status=${r.status}`);

  // 2. Verify OTP
  r = await req('POST', '/auth/otp/verify', { phoneNumber: PHONE, code: OTP_CODE });
  test('POST /auth/otp/verify', r.data.success === true && r.data.data?.accessToken,
    `isNew=${r.data.data?.isNewUser}`);
  TOKEN = r.data.data?.accessToken || '';
  REFRESH_TOKEN = r.data.data?.refreshToken || '';

  // 3. Get Current User (role is camelCase 'admin' in JSON due to StringEnumConverter)
  r = await req('GET', '/auth/me');
  const userRole = r.data.data?.role;
  test('GET /auth/me', r.data.success === true && userRole?.toLowerCase() === 'admin',
    `role=${userRole}, phone=${r.data.data?.phoneNumber}`);

  // 4. Update Profile
  r = await req('PATCH', '/auth/profile', { firstName: 'Admin', lastName: 'Test' });
  test('PATCH /auth/profile', r.data.success === true);

  // ========== CATEGORIES ==========
  console.log('\n── CATEGORIES ──');

  r = await req('GET', '/categories');
  const catCount = r.data.data?.length || 0;
  test('GET /categories', r.data.success === true && catCount >= 20, `count=${catCount}`);

  // ========== QUESTIONS / TICKETS ==========
  console.log('\n── QUESTIONS & TICKETS ──');

  r = await req('GET', '/admin/questions/tickets');
  const ticketCount = r.data.data?.length || 0;
  test('GET /admin/questions/tickets', r.data.success === true, `tickets=${ticketCount}`);

  // ========== SUBSCRIPTIONS ==========
  console.log('\n── SUBSCRIPTIONS ──');

  r = await req('GET', '/subscriptions/plans');
  const planCount = r.data.data?.length || 0;
  test('GET /subscriptions/plans', r.data.success === true && planCount >= 1,
    `plans=${planCount}, names=${r.data.data?.map(p => lt(p.name)).join(', ')}`);

  r = await req('GET', '/subscriptions/status');
  test('GET /subscriptions/status', r.data.success === true,
    `status=${r.data.data?.status}`);

  // ========== ANNOUNCEMENTS ==========
  console.log('\n── ANNOUNCEMENTS ──');

  r = await req('GET', '/announcements/active');
  test('GET /announcements/active', r.data.success === true || r.status === 200,
    `count=${r.data.data?.length ?? 0}`);

  // ========== PROGRESS ==========
  console.log('\n── PROGRESS ──');

  r = await req('GET', '/progress/dashboard');
  test('GET /progress/dashboard', r.data.success === true,
    `exams=${r.data.data?.totalExamsTaken}, questions=${r.data.data?.totalQuestionsPracticed}`);

  r = await req('GET', '/progress/categories');
  test('GET /progress/categories', r.data.success === true,
    `categories=${r.data.data?.length}`);

  // ========== EXAM: START ==========
  console.log('\n── EXAM MODE ──');

  // Temporarily increase daily exam limit to avoid hitting limit from previous runs
  await req('PUT', '/admin/settings', { key: 'free_daily_exam_limit', value: '100' });

  r = await req('POST', '/exams/start', { licenseCategory: 'AB' });
  if (r.data.success) {
    examSessionId = r.data.data?.id;
    const q = r.data.data?.questions || [];
    const hasCorrectAnswers = q.some(qn => qn.answerOptions?.some(a => a.isCorrect !== undefined && a.isCorrect !== null));
    test('POST /exams/start', true, `id=${examSessionId}, questions=${q.length}, timer=${r.data.data?.timeLimitMinutes}min`);
    test('  → 20 questions', q.length === 20);
    test('  → no correct answers exposed', !hasCorrectAnswers, hasCorrectAnswers ? 'LEAKED!' : 'safe');
    test('  → timer is 25min', r.data.data?.timeLimitMinutes === 25, `got ${r.data.data?.timeLimitMinutes}`);
    test('  → passingScore ≥ 18', r.data.data?.passingScore >= 18, `got ${r.data.data?.passingScore}`);
    test('  → expiresAt set', !!r.data.data?.expiresAt);

    // Submit a few answers
    if (q.length > 0) {
      const sq = q[0];
      const firstOption = sq.answerOptions?.[0];
      if (firstOption) {
        r = await req('POST', `/exams/${examSessionId}/answer`, {
          sessionQuestionId: sq.id, selectedAnswerId: firstOption.id, timeSpentSeconds: 5
        });
        test('POST /exams/{id}/answer', r.data.success === true);
      }
    }

    // Get active exam
    r = await req('GET', '/exams/active');
    test('GET /exams/active', r.status === 200);

    // Get session
    r = await req('GET', `/exams/${examSessionId}`);
    test('GET /exams/{id}', r.data.success === true, `status=${r.data.data?.status}`);

    // Complete exam
    r = await req('POST', `/exams/${examSessionId}/complete`);
    test('POST /exams/{id}/complete', r.data.success === true,
      `score=${r.data.data?.correctCount}/${r.data.data?.totalQuestions}, passed=${r.data.data?.passed}`);

    // Get result
    r = await req('GET', `/exams/${examSessionId}/result`);
    const hasAnswers = r.data.data?.questions?.some(q => q.correctAnswerId);
    test('GET /exams/{id}/result', r.data.success === true);
    test('  → correct answers in result', hasAnswers === true, hasAnswers ? 'yes' : 'missing!');

  } else {
    test('POST /exams/start', false, r.data.error?.message);
  }

  // ========== EXAM HISTORY ==========
  r = await req('GET', '/exams/history');
  test('GET /exams/history', r.status === 200, `count=${r.data.data?.items?.length}`);

  // ========== TICKET EXAM ==========
  console.log('\n── TICKET EXAM ──');

  r = await req('POST', '/exams/start-ticket', { ticketNumber: 1 });
  if (r.data.success) {
    ticketExamId = r.data.data?.id;
    test('POST /exams/start-ticket', true, `id=${ticketExamId}, questions=${r.data.data?.questions?.length}, ticket=${r.data.data?.ticketNumber}`);
    test('  → timer is 25min', r.data.data?.timeLimitMinutes === 25, `got ${r.data.data?.timeLimitMinutes}`);
    test('  → passingScore ≥ 18', r.data.data?.passingScore >= 18, `got ${r.data.data?.passingScore}`);

    // Abandon to free up for marathon
    r = await req('POST', `/exams/${ticketExamId}/abandon`);
    test('POST /exams/{id}/abandon', r.data.success === true);
  } else {
    test('POST /exams/start-ticket', false, r.data.error?.message);
  }

  // ========== MARATHON ==========
  console.log('\n── MARATHON ──');

  r = await req('POST', '/exams/start-marathon', {});
  if (r.data.success) {
    marathonId = r.data.data?.id;
    const mq = r.data.data?.questions || [];
    const totalQ = r.data.data?.totalQuestions || 0;
    test('POST /exams/start-marathon', true, `id=${marathonId}, batchQuestions=${mq.length}, totalQuestions=${totalQ}`);
    // Marathon returns first batch of 20 questions via .Take(20) — by design
    test('  → first batch ≤ 20 questions', mq.length <= 20 && mq.length > 0, `got ${mq.length}`);
    test('  → totalQuestions > batch', totalQ >= mq.length, `total=${totalQ}, batch=${mq.length}`);
    // expiresAt is null → omitted by JsonIgnoreCondition.WhenWritingNull → undefined in JS
    test('  → no timer (expiresAt omitted)', r.data.data?.expiresAt === undefined || r.data.data?.expiresAt === null,
      `expiresAt=${r.data.data?.expiresAt}`);

    // Abandon marathon
    r = await req('POST', `/exams/${marathonId}/abandon`);
    test('POST /exams/marathon/abandon', r.data.success === true);
  } else {
    test('POST /exams/start-marathon', false, r.data.error?.message);
  }

  // ========== PRACTICE ==========
  console.log('\n── PRACTICE ──');

  r = await req('GET', '/practice/session?batchSize=10');
  test('GET /practice/session', r.data.success === true, `questions=${r.data.data?.questions?.length}`);
  const practiceQ = r.data.data?.questions;

  if (practiceQ?.length > 0) {
    const pq = practiceQ[0];
    const firstAns = pq.answerOptions?.[0];
    if (firstAns) {
      // Practice session question uses 'id' field, not 'questionId'
      r = await req('POST', '/practice/answer', {
        questionId: pq.id, selectedAnswerId: firstAns.id, timeSpentSeconds: 3
      });
      // Response is PracticeAnswerFeedbackDto: isCorrect, correctAnswerId, explanation, newLeitnerBox, nextReviewDate
      const fb = r.data.data;
      test('POST /practice/answer', r.data.success === true,
        `correct=${fb?.isCorrect}, leitnerBox=${fb?.newLeitnerBox}`);
      test('  → has correctAnswerId', fb?.correctAnswerId !== undefined && fb?.correctAnswerId !== null);
      test('  → has newLeitnerBox', fb?.newLeitnerBox !== undefined, `box=${fb?.newLeitnerBox}`);
    }
  }

  r = await req('GET', '/practice/due-count');
  test('GET /practice/due-count', r.data.success === true, `due=${r.data.data?.dueCount}`);

  // ========== ADMIN SETTINGS ==========
  console.log('\n── ADMIN SETTINGS ──');

  r = await req('GET', '/admin/settings');
  test('GET /admin/settings', r.data.success === true,
    `keys=${r.data.data?.map(s => s.key).join(', ')}`);

  r = await req('PUT', '/admin/settings', { key: 'free_daily_exam_limit', value: '5' });
  test('PUT /admin/settings (free_daily_exam_limit=5)', r.data.success === true);

  // Reset it back to default
  r = await req('PUT', '/admin/settings', { key: 'free_daily_exam_limit', value: '3' });
  test('PUT /admin/settings (reset to 3)', r.data.success === true);

  // Also reset the limit we bumped for exam tests
  await req('PUT', '/admin/settings', { key: 'free_daily_exam_limit', value: '3' });

  // ========== ADMIN QUESTIONS ==========
  console.log('\n── ADMIN QUESTIONS ──');

  r = await req('GET', '/admin/questions?page=1&pageSize=5');
  test('GET /admin/questions', r.status === 200, `total=${r.data.data?.totalCount}`);

  if (r.data.data?.items?.length > 0) {
    questionId = r.data.data.items[0].id;

    // Toggle status — deactivate
    r = await req('PATCH', `/admin/questions/${questionId}/status`, { isActive: false });
    test('PATCH /admin/questions/{id}/status (deactivate)', r.data.success === true);

    // Re-activate
    r = await req('PATCH', `/admin/questions/${questionId}/status`, { isActive: true });
    test('PATCH /admin/questions/{id}/status (reactivate)', r.data.success === true);
  }

  // Export template
  const exportRes = await fetch(`${API}/admin/questions/export-template`, {
    headers: { 'Authorization': `Bearer ${TOKEN}` }
  });
  test('GET /admin/questions/export-template', exportRes.status === 200,
    `contentType=${exportRes.headers.get('content-type')}, size=${exportRes.headers.get('content-length')}bytes`);

  // ========== ADMIN DASHBOARD ==========
  console.log('\n── ADMIN DASHBOARD ──');

  r = await req('GET', '/admin/dashboard');
  test('GET /admin/dashboard', r.data.success === true,
    `users=${r.data.data?.totalUsers}, questions=${r.data.data?.totalQuestions}`);

  // ========== ADMIN ANNOUNCEMENTS ==========
  console.log('\n── ADMIN ANNOUNCEMENTS ──');

  r = await req('GET', '/admin/announcements?page=1&pageSize=10');
  test('GET /admin/announcements', r.status === 200);

  // Create announcement — command expects FLAT fields: titleUz, titleUzLatin, titleRu, contentUz, etc.
  r = await req('POST', '/admin/announcements', {
    titleUz: "Тест эълон",
    titleUzLatin: "Test e'lon",
    titleRu: "Тестовое объявление",
    contentUz: "Тест мазмун",
    contentUzLatin: "Test mazmun",
    contentRu: "Тестовое содержание",
    type: "Info",
    isActive: true
  });
  let announcementId = '';
  if (r.data.success) {
    announcementId = r.data.data?.id || '';
    test('POST /admin/announcements (create)', true, `id=${announcementId}`);
  } else {
    test('POST /admin/announcements (create)', false,
      `status=${r.status}, error=${r.data.error?.message || JSON.stringify(r.data)}`);
  }

  // Delete announcement to clean up (if created)
  if (announcementId) {
    r = await req('DELETE', `/admin/announcements/${announcementId}`);
    test('DELETE /admin/announcements/{id}', r.data.success === true);
  }

  // ========== ADMIN USERS ==========
  console.log('\n── ADMIN USERS ──');

  r = await req('GET', '/admin/users?page=1&pageSize=10');
  test('GET /admin/users', r.status === 200, `total=${r.data.data?.totalCount}`);

  // ========== ADMIN AUDIT LOG ==========
  console.log('\n── ADMIN AUDIT LOG ──');

  // Route is /admin/audit-logs (PLURAL with 's')
  r = await req('GET', '/admin/audit-logs?page=1&pageSize=10');
  test('GET /admin/audit-logs', r.status === 200);

  // ========== ADMIN EXAM TEMPLATES ==========
  console.log('\n── ADMIN EXAM TEMPLATES ──');

  r = await req('GET', '/admin/exam-templates');
  test('GET /admin/exam-templates', r.data.success === true || r.status === 200,
    `count=${r.data.data?.length}`);

  // ========== ADMIN CATEGORIES ==========
  console.log('\n── ADMIN CATEGORIES ──');

  r = await req('GET', '/admin/categories');
  test('GET /admin/categories', r.status === 200);

  // ========== ADMIN PLANS ==========
  console.log('\n── ADMIN PLANS ──');

  r = await req('GET', '/admin/plans');
  test('GET /admin/plans', r.status === 200);

  // ========== REFRESH TOKEN ==========
  console.log('\n── TOKEN REFRESH ──');

  // Use saved refresh token from initial login
  if (REFRESH_TOKEN) {
    r = await req('POST', '/auth/refresh', { refreshToken: REFRESH_TOKEN });
    test('POST /auth/refresh', r.data.success === true && r.data.data?.accessToken,
      `newToken=${r.data.data?.accessToken ? 'received' : 'missing'}`);
    if (r.data.data?.accessToken) TOKEN = r.data.data.accessToken;
    if (r.data.data?.refreshToken) REFRESH_TOKEN = r.data.data.refreshToken;
  } else {
    test('POST /auth/refresh', false, 'no refresh token saved from initial verify');
  }

  // ========== LOGOUT ==========
  console.log('\n── LOGOUT ──');
  // Logout requires refreshToken in body. After refresh, we get a new refresh token.
  const logoutRefreshToken = r.data.data?.refreshToken || REFRESH_TOKEN;
  r = await req('POST', '/auth/logout', { refreshToken: logoutRefreshToken });
  test('POST /auth/logout', r.data.success === true);

  // ========== SUMMARY ==========
  console.log('\n══════════════════════════════════');
  const passed = results.filter(r => r.status === '✅').length;
  const failed = results.filter(r => r.status === '❌').length;
  console.log(`\n📊 RESULTS: ${passed} passed, ${failed} failed out of ${results.length} tests`);

  if (failed > 0) {
    console.log('\n❌ FAILED TESTS:');
    results.filter(r => r.status === '❌').forEach(r => console.log(`  - ${r.name}: ${r.detail}`));
  }

  console.log('\n🏁 E2E TEST COMPLETE');
}

run().catch(e => console.error('Fatal:', e));
