import type { GetQueryResponse } from "../types/Get.ts";
import { faker } from "@faker-js/faker";

 /**
 * @description OK
 */
export function createGet200() {
    return undefined;
}

 export function createGetQueryResponse(data?: Partial<GetQueryResponse>) {
    return faker.helpers.arrayElement<any>([createGet200()]) || data;
}