import { HttpInterceptorFn } from '@angular/common/http';
import { env } from './env';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const headers = req.headers.set('X-Api-Key', env.apiKey);
  return next(req.clone({ headers }));
};
