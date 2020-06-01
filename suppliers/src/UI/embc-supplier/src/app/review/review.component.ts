import { Component, OnInit } from '@angular/core';
import { SupplierService } from '../service/supplier.service';
import { Router } from '@angular/router';
import { SupplierHttpService } from '../service/supplierHttp.service';
import * as globalConst from 'src/app/service/globalConstants'

@Component({
    selector: 'review',
    templateUrl: './review.component.html',
    styleUrls: ['./review.component.scss']
})
export class ReviewComponent implements OnInit{

    supplierSubmissionType: string;
    supplier: any;
    captchaVerified = false;
    isError =false;
    captchaFilled = false;
    errorMessage: string;
    isSubmitted: boolean = false;
   
    constructor(public supplierService: SupplierService, private router: Router, private httpService: SupplierHttpService) { }

    ngOnInit() {
        this.supplier = this.supplierService.getSupplierDetails();
        this.supplierSubmissionType = this.supplier.supplierSubmissionType;
        console.log(this.supplier);
    }

    goback() {
        this.router.navigate(['/submission']);
    }

    submit() {
        this.isSubmitted = true;
        if(!this.captchaFilled) {
            this.errorMessage = globalConst.captchaErr;
            this.isError =true;
            this.isSubmitted = false;
        } else {
            this.httpService.submitForm(this.supplierService.getPayload()).subscribe((res: any) => {
                this.supplierService.setReferenceNumber(res);
                this.router.navigate(['/thankyou']);
            },
            (error: any) => {
                this.isError =true;
                this.isSubmitted = false;
                if(error.detail && error.detail !== "") {
                    this.errorMessage = error.detail
                } else {
                    this.errorMessage = globalConst.appSubmitErr;
                }
            })
        }
    }

    public onValidToken(token: any) {
        console.log('Valid token received: ', token);
        this.captchaVerified = true;
        this.captchaFilled = true;
      }
    
      public onServerError(error: any) {
        console.log('Server error: ', error);
        this.captchaVerified = true;
        this.captchaFilled = true;
      }

}