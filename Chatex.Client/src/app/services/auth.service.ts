import { Injectable } from '@angular/core';
import { SignInRequest } from '../models/signin.request';
import { map, Observable } from 'rxjs';
import { SignInResponse } from '../models/signin.response';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly key: string = 'token';
  private _userId: string | null = null;

  constructor(private http: HttpClient) { }

  /**
   * Sends credentials to server for validation
   */
  signIn(credentials: SignInRequest): Observable<SignInResponse> {    
    return this.http.post<SignInResponse>('api/auth/signin', credentials).pipe(
      map((response) => {        
        localStorage.setItem(this.key, response.token);
        this._userId = response.userId;
        return response;
      }),
    );
  }
}
