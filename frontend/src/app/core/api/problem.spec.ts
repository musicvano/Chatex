import { HttpErrorResponse } from '@angular/common/http';
import { fieldErrors, problemMessage, toProblem } from './problem';

function httpError(status: number, body: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body });
}

describe('problem', () => {
  it('reads code, detail and camel-cases field errors', () => {
    const problem = toProblem(
      httpError(400, {
        code: 'validation_failed',
        detail: 'Bad input',
        errors: { UserName: ['Too short.'], password: ['Needs a digit.'] },
      }),
    );

    expect(problem.status).toBe(400);
    expect(problem.code).toBe('validation_failed');
    expect(problem.errors).toEqual({ userName: ['Too short.'], password: ['Needs a digit.'] });
  });

  it('turns known codes into friendly text', () => {
    expect(problemMessage(httpError(409, { code: 'user_name_taken' }))).toBe(
      'This user name is already taken.',
    );
  });

  it('falls back to detail, then the first field error', () => {
    expect(problemMessage(httpError(400, { code: 'x', detail: 'Server says no' }))).toBe('Server says no');
    expect(problemMessage(httpError(400, { errors: { title: ['Title is required.'] } }))).toBe(
      'Title is required.',
    );
  });

  it('explains network failures', () => {
    expect(problemMessage(httpError(0, null))).toContain("Can't reach the server");
  });

  it('returns the errors of one field', () => {
    const error = httpError(400, { errors: { NewPassword: ['Too weak.'] } });
    expect(fieldErrors(error, 'newPassword')).toEqual(['Too weak.']);
    expect(fieldErrors(error, 'other')).toEqual([]);
  });
});
