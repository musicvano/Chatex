import { Component } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { SignInRequest } from '../models/signin.request';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-signin',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './signin.component.html',
})
export class SigninComponent {
  form: FormGroup<{
    email: FormControl<string | null>;
    password: FormControl<string | null>;
  }>;

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.form = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]],
    });
  }

  onSubmit() {
    if (!this.form.valid) return;
    const credentials: SignInRequest = {
      email: this.form.value.email!,
      password: this.form.value.password!,
    };
    this.authService.signIn(credentials).subscribe({
      next: (response) => {
        this.router.navigate(['chats']);
      },
      error: (response) => {
      },
    });
  }
}
