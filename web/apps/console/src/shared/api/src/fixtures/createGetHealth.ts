import type { GetHealthQueryResponse } from "../types/GetHealth.ts";
import { faker } from "@faker-js/faker";

 /**
 * @description OK
 */
export function createGetHealth200() {
    return undefined;
}

 export function createGetHealthQueryResponse(data?: Partial<GetHealthQueryResponse>) {
    return faker.helpers.arrayElement<any>([createGetHealth200()]) || data;
}